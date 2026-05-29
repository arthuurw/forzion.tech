using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using forzion.tech.Application.UseCases.Pagamentos.ReconciliarPagamentosStripe;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Pagamentos;

public class ReconciliarPagamentosStripeHandlerTests
{
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IContaRecebimentoRepository> _contaRecebimentoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<ProcessarWebhookStripeHandler>> _webhookLogger = new();
    private readonly Mock<ILogger<ReconciliarPagamentosStripeHandler>> _reconciliarLogger = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly ProcessarWebhookStripeHandler _webhookHandler;
    private readonly ReconciliarPagamentosStripeHandler _handler;

    public ReconciliarPagamentosStripeHandlerTests()
    {
        // Usa o handler REAL — ProcessarEventoAsync passa por toda a lógica original
        // (incluindo idempotência e cross-account), só pula a verificação de assinatura.
        // Isso garante que a reconciliação compartilha o mesmo código do webhook live.
        _webhookHandler = new ProcessarWebhookStripeHandler(
            _pagamentoRepo.Object, _assinaturaRepo.Object, _contaRecebimentoRepo.Object,
            _stripeService.Object, _unitOfWork.Object, _time, _webhookLogger.Object);

        _handler = new ReconciliarPagamentosStripeHandler(
            _stripeService.Object, _webhookHandler, _time, _reconciliarLogger.Object);
    }

    private static StripeEventSummary PaymentIntentEvento(string type, string paymentIntentId, DateTime created, string? accountId = null)
    {
        var payload = accountId is null
            ? "{\"type\":\"" + type + "\",\"data\":{\"object\":{\"id\":\"" + paymentIntentId + "\"}}}"
            : "{\"type\":\"" + type + "\",\"account\":\"" + accountId + "\",\"data\":{\"object\":{\"id\":\"" + paymentIntentId + "\"}}}";
        return new StripeEventSummary("evt_" + paymentIntentId, type, payload, created);
    }

    private void SetupEventos(params StripeEventSummary[] eventos)
    {
        _stripeService
            .Setup(s => s.ListarEventosDesdeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventos);
    }

    [Fact]
    public async Task HandleAsync_ListaVazia_RetornaContadoresZero()
    {
        SetupEventos();

        var result = await _handler.HandleAsync(new ReconciliarPagamentosStripeCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEventos.Should().Be(0);
        result.Value.Replayed.Should().Be(0);
        result.Value.JaConsistentes.Should().Be(0);
        result.Value.Erros.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_DesdeUtcNulo_UsaJanelaDeSeteDias()
    {
        SetupEventos();
        var agora = _time.GetUtcNow().UtcDateTime;
        var esperadoDesde = agora.AddDays(-7);

        var result = await _handler.HandleAsync(new ReconciliarPagamentosStripeCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.DesdeUtc.Should().BeCloseTo(esperadoDesde, TimeSpan.FromSeconds(1));
        _stripeService.Verify(s => s.ListarEventosDesdeAsync(
            It.Is<DateTime>(d => Math.Abs((d - esperadoDesde).TotalSeconds) < 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DesdeUtcExplicito_PropagaParaStripeService()
    {
        var desde = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        SetupEventos();

        var result = await _handler.HandleAsync(new ReconciliarPagamentosStripeCommand(desde));

        result.IsSuccess.Should().BeTrue();
        _stripeService.Verify(s => s.ListarEventosDesdeAsync(desde, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_PagamentoPendente_Aplica()
    {
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, _time.GetUtcNow().UtcDateTime);
        pagamento.DefinirDadosPix("pi_replay", "qr", "url", _time.GetUtcNow().UtcDateTime.AddHours(1));
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, _time.GetUtcNow().UtcDateTime);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_replay", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        SetupEventos(PaymentIntentEvento("payment_intent.succeeded", "pi_replay", _time.GetUtcNow().UtcDateTime));

        var result = await _handler.HandleAsync(new ReconciliarPagamentosStripeCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEventos.Should().Be(1);
        result.Value.Replayed.Should().Be(1);
        result.Value.JaConsistentes.Should().Be(0);
        result.Value.Erros.Should().Be(0);
        pagamento.Status.Should().Be(PagamentoStatus.Pago);
        assinatura.Status.Should().Be(AssinaturaAlunoStatus.Ativa);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_PagamentoJaPago_Idempotente()
    {
        // Webhook já tinha sido processado; reconciliação encontra mesmo evento e não muta.
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 150m, _time.GetUtcNow().UtcDateTime);
        pagamento.DefinirDadosPix("pi_idem", "qr", "url", _time.GetUtcNow().UtcDateTime.AddHours(1));
        pagamento.MarcarPago();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_idem", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        SetupEventos(PaymentIntentEvento("payment_intent.succeeded", "pi_idem", _time.GetUtcNow().UtcDateTime));

        var result = await _handler.HandleAsync(new ReconciliarPagamentosStripeCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.Replayed.Should().Be(0);
        result.Value.JaConsistentes.Should().Be(1);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentFailed_PagamentoPendente_Aplica()
    {
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 150m, _time.GetUtcNow().UtcDateTime);
        pagamento.DefinirDadosPix("pi_failure", "qr", "url", _time.GetUtcNow().UtcDateTime.AddHours(1));

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_failure", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);
        SetupEventos(PaymentIntentEvento("payment_intent.payment_failed", "pi_failure", _time.GetUtcNow().UtcDateTime));

        var result = await _handler.HandleAsync(new ReconciliarPagamentosStripeCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.Replayed.Should().Be(1);
        pagamento.Status.Should().Be(PagamentoStatus.Falhou);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentDesconhecido_ContaComoJaConsistente()
    {
        // Pagamento sumiu da base (deletado/nunca criado) — não conseguimos aplicar nada,
        // contabiliza como JaConsistente. Documentado em ProcessarEventoResultado.
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        SetupEventos(PaymentIntentEvento("payment_intent.succeeded", "pi_ghost", _time.GetUtcNow().UtcDateTime));

        var result = await _handler.HandleAsync(new ReconciliarPagamentosStripeCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.Replayed.Should().Be(0);
        result.Value.JaConsistentes.Should().Be(1);
        result.Value.Erros.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_CrossAccount_NaoReplayENaoMuta()
    {
        // Defesa cross-account: account_id no evento ≠ account_id configurado pro treinador
        // → ProcessarEventoAsync devolve JaConsistente sem mutar nada.
        var treinadorId = Guid.NewGuid();
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, _time.GetUtcNow().UtcDateTime);
        pagamento.DefinirDadosPix("pi_xacct", "qr", "url", _time.GetUtcNow().UtcDateTime.AddHours(1));
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m, _time.GetUtcNow().UtcDateTime);
        var conta = ContaRecebimento.Criar(treinadorId, _time.GetUtcNow().UtcDateTime);
        conta.ConfigurarStripeConnect("acct_correct");

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_xacct", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        SetupEventos(PaymentIntentEvento("payment_intent.succeeded", "pi_xacct", _time.GetUtcNow().UtcDateTime, accountId: "acct_attacker"));

        var result = await _handler.HandleAsync(new ReconciliarPagamentosStripeCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.Replayed.Should().Be(0);
        result.Value.JaConsistentes.Should().Be(1);
        pagamento.Status.Should().Be(PagamentoStatus.Pendente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EventoComPayloadInvalido_ContaComoErro()
    {
        // Payload malformado dispara InvalidOperationException no parser — handler captura,
        // loga, conta como Erro e prossegue varredura.
        var evtRuim = new StripeEventSummary("evt_bad", "payment_intent.succeeded", "null", _time.GetUtcNow().UtcDateTime);
        SetupEventos(evtRuim);

        var result = await _handler.HandleAsync(new ReconciliarPagamentosStripeCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEventos.Should().Be(1);
        result.Value.Erros.Should().Be(1);
        result.Value.Replayed.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_VariosEventos_ProcessaTodosEAgrega()
    {
        // 3 eventos: 1 replay, 1 idempotente, 1 ghost.
        var pagPendente = Pagamento.Criar(Guid.NewGuid(), 100m, _time.GetUtcNow().UtcDateTime);
        pagPendente.DefinirDadosPix("pi_a", "qr", "url", _time.GetUtcNow().UtcDateTime.AddHours(1));
        var pagJaPago = Pagamento.Criar(Guid.NewGuid(), 100m, _time.GetUtcNow().UtcDateTime);
        pagJaPago.DefinirDadosPix("pi_b", "qr", "url", _time.GetUtcNow().UtcDateTime.AddHours(1));
        pagJaPago.MarcarPago();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagPendente);
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_b", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagJaPago);
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_c", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);

        SetupEventos(
            PaymentIntentEvento("payment_intent.succeeded", "pi_a", _time.GetUtcNow().UtcDateTime),
            PaymentIntentEvento("payment_intent.succeeded", "pi_b", _time.GetUtcNow().UtcDateTime),
            PaymentIntentEvento("payment_intent.succeeded", "pi_c", _time.GetUtcNow().UtcDateTime));

        var result = await _handler.HandleAsync(new ReconciliarPagamentosStripeCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEventos.Should().Be(3);
        result.Value.Replayed.Should().Be(1);
        result.Value.JaConsistentes.Should().Be(2);
        result.Value.Erros.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
