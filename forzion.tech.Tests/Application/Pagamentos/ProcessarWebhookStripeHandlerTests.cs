using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Pagamentos;

public class ProcessarWebhookStripeHandlerTests
{
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IContaRecebimentoRepository> _contaRecebimentoRepo = new();
    private readonly Mock<IPagamentoTreinadorRepository> _pagamentoTreinadorRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<ProcessarWebhookStripeHandler>> _logger = new();
    private readonly ProcessarWebhookStripeHandler _handler;

    private const string ValidSig = "t=1,v1=abc";

    public ProcessarWebhookStripeHandlerTests()
    {
        _handler = new ProcessarWebhookStripeHandler(
            _pagamentoRepo.Object, _assinaturaRepo.Object, _contaRecebimentoRepo.Object,
            _pagamentoTreinadorRepo.Object, _stripeService.Object, _unitOfWork.Object, TimeProvider.System, _logger.Object);

        _stripeService.Setup(s => s.ValidarWebhookAsync(It.IsAny<string>(), ValidSig))
            .ReturnsAsync(true);
    }

    private static string PaymentIntentPayload(string type, string paymentIntentId) =>
        "{\"type\":\"" + type + "\",\"data\":{\"object\":{\"id\":\"" + paymentIntentId + "\"}}}";

    private static string PaymentIntentPayloadComConnectAccount(string type, string paymentIntentId, string accountId) =>
        "{\"type\":\"" + type + "\",\"account\":\"" + accountId + "\",\"data\":{\"object\":{\"id\":\"" + paymentIntentId + "\"}}}";

    private static string AccountPayload(string accountId, bool chargesEnabled) =>
        "{\"type\":\"account.updated\",\"account\":\"" + accountId + "\",\"data\":{\"object\":{\"charges_enabled\":" + (chargesEnabled ? "true" : "false") + "}}}";

    private static string PaymentIntentTreinadorPayload(string type, string paymentIntentId) =>
        "{\"type\":\"" + type + "\",\"data\":{\"object\":{\"id\":\"" + paymentIntentId + "\",\"metadata\":{\"tipo\":\"plano_treinador\"}}}}";

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_PlanoTreinador_RoteiaParaPagamentoTreinador()
    {
        var pagamento = PagamentoTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), 50m, FinalidadePagamentoTreinador.Cadastro, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_treinador", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);
        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_treinador", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentTreinadorPayload("payment_intent.succeeded", "pi_treinador"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pago);
        _pagamentoRepo.Verify(r => r.ObterPorPaymentIntentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never, "pagamento de treinador não passa pelo fluxo de aluno");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaAlunoInvalida_RetornaFailure()
    {
        _stripeService.Setup(s => s.ValidarWebhookAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var result = await _handler.HandleAsync(new ProcessarWebhookStripeCommand("{}", "bad_sig"));

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_MarcaPagoEAgendaRenovacaoEAtiva()
    {
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_abc", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, DateTime.UtcNow).Value;

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.succeeded", "pi_abc"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pago);
        assinatura.Status.Should().Be(AssinaturaAlunoStatus.Ativa);
        assinatura.DataProximaCobranca.Should().BeAfter(DateTime.UtcNow);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_Idempotente_NaoRelancaExcecaoNemComita()
    {
        // Stripe re-entrega webhook para pagamento já confirmado — deve retornar sucesso silencioso
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_dup", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora); // já processado

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_dup", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.succeeded", "pi_dup"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentFailed_Idempotente_NaoRelancaExcecao()
    {
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_f2", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarFalhou(TestData.Agora); // já processado

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_f2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.payment_failed", "pi_f2"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentCanceled_Idempotente_NaoRelancaExcecao()
    {
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_e2", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarExpirado(TestData.Agora); // já processado

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_e2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.canceled", "pi_e2"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_AssinaturaAlunoNaoEncontrada_MarcaPagoSemAtivar()
    {
        // Cenário: pagamento confirmado mas assinatura foi excluída/não existe — dinheiro chegou, registra Pago
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_orphan", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_orphan", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.succeeded", "pi_orphan"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pago);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentFailed_MarcaFalhou()
    {
        // Sem assinatura encontrada — só commita o MarcarFalhou (não há contador a incrementar).
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_fail", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_fail", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.payment_failed", "pi_fail"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Falhou);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentFailed_AssinaturaAtivaTerceiraFalha_MarcaInadimplente()
    {
        // G-PAY-2: pagamento + assinatura mutados antes do único CommitAsync — sem janela de dessinc.
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_3rd_fail", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        assinatura.Ativar(TestData.Agora);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
        assinatura.ClearDomainEvents();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_3rd_fail", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.payment_failed", "pi_3rd_fail"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Falhou);
        assinatura.Status.Should().Be(AssinaturaAlunoStatus.Inadimplente);
        assinatura.TentativasFalhasConsecutivas.Should().Be(3);
        // G-PAY-2: single commit — pagamento + assinatura num único CommitAsync.
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentFailed_AssinaturaInadimplenteQuartaFalha_MantemInadimplente()
    {
        // Já Inadimplente; quarta falha apenas incrementa contador e dispara PagamentoFalhouEvent.
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_4th_fail", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        assinatura.Ativar(TestData.Agora);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow); // → Inadimplente
        assinatura.ClearDomainEvents();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_4th_fail", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.payment_failed", "pi_4th_fail"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Falhou);
        assinatura.Status.Should().Be(AssinaturaAlunoStatus.Inadimplente);
        assinatura.TentativasFalhasConsecutivas.Should().Be(4);
        // G-PAY-2: single commit.
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_AssinaturaInadimplente_Regulariza_VoltaParaAtiva()
    {
        // Sucesso em assinatura Inadimplente → Regulariza (Ativa + contador 0).
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_regul", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        assinatura.Ativar(TestData.Agora);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow); // → Inadimplente
        assinatura.ClearDomainEvents();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_regul", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.succeeded", "pi_regul"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pago);
        assinatura.Status.Should().Be(AssinaturaAlunoStatus.Ativa);
        assinatura.TentativasFalhasConsecutivas.Should().Be(0);
        assinatura.DataProximaCobranca.Should().BeAfter(DateTime.UtcNow);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_AssinaturaAtivaComFalhasParciais_ZeraContador()
    {
        // Assinatura Ativa com 2 falhas (abaixo do threshold) e pagamento sucesso → contador zerado, permanece Ativa.
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_zero", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        assinatura.Ativar(TestData.Agora);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
        assinatura.ClearDomainEvents();
        assinatura.TentativasFalhasConsecutivas.Should().Be(2);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_zero", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.succeeded", "pi_zero"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pago);
        assinatura.Status.Should().Be(AssinaturaAlunoStatus.Ativa);
        assinatura.TentativasFalhasConsecutivas.Should().Be(0);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentCanceled_MarcaExpirado()
    {
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_can", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_can", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.canceled", "pi_can"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Expirado);
    }

    [Fact]
    public async Task HandleAsync_AccountUpdatedComChargesEnabled_ConfirmaOnboarding()
    {
        var contaRecebimento = ContaRecebimento.Criar(Guid.NewGuid(), DateTime.UtcNow).Value;
        contaRecebimento.ConfigurarStripeConnect("acct_ok", TestData.Agora);
        _contaRecebimentoRepo.Setup(r => r.ObterPorStripeAccountIdAsync("acct_ok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(AccountPayload("acct_ok", true), ValidSig));

        result.IsSuccess.Should().BeTrue();
        contaRecebimento.OnboardingCompleto.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AccountUpdatedSemChargesEnabled_NaoConfirma()
    {
        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(AccountPayload("acct_x", false), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentNaoEncontrado_RetornaSucessoSemCommit()
    {
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.succeeded", "pi_unknown"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EventoDesconhecido_RetornaSucesso()
    {
        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand("""{"type":"unknown.event","data":{"object":{}}}""", ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_PaymentIntent_ConnectAccountBate_ProcessaNormal()
    {
        var treinadorId = Guid.NewGuid();
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_ok_acct", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        var conta = ContaRecebimento.Criar(treinadorId, DateTime.UtcNow).Value;
        conta.ConfigurarStripeConnect("acct_correct", TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_ok_acct", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        var result = await _handler.HandleAsync(new ProcessarWebhookStripeCommand(
            PaymentIntentPayloadComConnectAccount("payment_intent.succeeded", "pi_ok_acct", "acct_correct"),
            ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pago);
    }

    private static string ChargeRefundedPayload(string paymentIntentId, long amountRefundedCents = 14990) =>
        "{\"type\":\"charge.refunded\",\"data\":{\"object\":{\"id\":\"ch_x\",\"payment_intent\":\"" + paymentIntentId + "\",\"amount_refunded\":" + amountRefundedCents + ",\"refunded\":true}}}";

    [Fact]
    public async Task HandleAsync_ChargeRefunded_PagamentoPago_MarcaEstornadoEComita()
    {
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_refund", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.ClearDomainEvents();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_refund", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeRefundedPayload("pi_refund"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Estornado);
        pagamento.DomainEvents.Should().ContainSingle(e => e is forzion.tech.Domain.Events.PagamentoEstornadoEvent);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChargeRefunded_JaEstornado_Idempotente_NaoComita()
    {
        // Stripe re-entrega webhook charge.refunded — handler deve absorver silenciosamente.
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_dup_refund", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.MarcarEstornado(TestData.Agora); // já processado

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_dup_refund", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeRefundedPayload("pi_dup_refund"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Estornado);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeRefunded_PagamentoNaoEncontrado_NaoComita()
    {
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_ghost", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeRefundedPayload("pi_ghost"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeRefunded_PagamentoPendente_NaoMutaEComitaNao()
    {
        // Estado inconsistente: refund de Pagamento Pendente. Stripe não deveria mandar
        // mas o defensivo é warn + no-op — não estourar exception (Stripe retentaria).
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_pend_refund", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_pend_refund", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeRefundedPayload("pi_pend_refund"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pendente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeRefunded_SemPaymentIntent_NaoComita()
    {
        const string payload = "{\"type\":\"charge.refunded\",\"data\":{\"object\":{\"id\":\"ch_x\",\"amount_refunded\":100}}}";

        var result = await _handler.HandleAsync(new ProcessarWebhookStripeCommand(payload, ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeRefunded_RefundParcial_MantemStatusPagoENaoComita()
    {
        // G-PAY-5: refund parcial (5000 cents < 14990 cents) não deve alterar status do pagamento.
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_partial_refund", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.ClearDomainEvents();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_partial_refund", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        // Partial: 50,00 de 149,90 reembolsados
        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeRefundedPayload("pi_partial_refund", 5000), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pago, "refund parcial não deve alterar status");
        pagamento.DomainEvents.OfType<forzion.tech.Domain.Events.PagamentoEstornadoEvent>().Should().BeEmpty();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeRefunded_RefundTotal_MarcaEstornado()
    {
        // G-PAY-5: refund total (amountRefundedCents == valor * 100) deve marcar Estornado.
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_full_refund", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.ClearDomainEvents();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_full_refund", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        // Full: 149,90 → 14990 centavos
        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeRefundedPayload("pi_full_refund", 14990), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Estornado);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChargeRefunded_AmountRefundedAusente_MarcaEstornado()
    {
        // G-PAY-5: quando amountRefundedCents é null (campo ausente no payload), tratamos
        // como refund total para não silenciar eventos legítimos (comportamento conservador).
        const string payloadSemAmount =
            "{\"type\":\"charge.refunded\",\"data\":{\"object\":{\"id\":\"ch_x\",\"payment_intent\":\"pi_no_amount\"}}}";

        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_no_amount", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.ClearDomainEvents();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_no_amount", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(new ProcessarWebhookStripeCommand(payloadSemAmount, ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Estornado, "sem amount_refunded tratamos como total");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static string ChargeDisputeCreatedPayload(string paymentIntentId, string motivo = "fraudulent") =>
        "{\"type\":\"charge.dispute.created\",\"data\":{\"object\":{\"id\":\"dp_x\",\"payment_intent\":\"" + paymentIntentId + "\",\"reason\":\"" + motivo + "\",\"amount\":14990}}}";

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_PagamentoPago_MarcaEmDisputaEForcaAssinaturaInadimplente()
    {
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_dispute", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.ClearDomainEvents();

        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        assinatura.Ativar(TestData.Agora);
        assinatura.ClearDomainEvents();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_dispute", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeCreatedPayload("pi_dispute"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.EmDisputa);
        // Drástico: assinatura é congelada independente do contador prévio.
        assinatura.Status.Should().Be(AssinaturaAlunoStatus.Inadimplente);
        assinatura.TentativasFalhasConsecutivas.Should().Be(AssinaturaAluno.LimiteTentativasFalhas);
        pagamento.DomainEvents.Should().ContainSingle(e => e is forzion.tech.Domain.Events.PagamentoEmDisputaEvent);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_PropagaMotivoParaEvento()
    {
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_dispute_reason", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.ClearDomainEvents();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_dispute_reason", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeCreatedPayload("pi_dispute_reason", "duplicate"), ValidSig));

        pagamento.DomainEvents.OfType<forzion.tech.Domain.Events.PagamentoEmDisputaEvent>().Single()
            .MotivoDisputa.Should().Be("duplicate");
    }

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_JaEmDisputa_Idempotente_NaoComita()
    {
        // Stripe re-entrega webhook charge.dispute.created — handler absorve silenciosamente.
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_dup_dispute", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.MarcarEmDisputa("fraudulent", TestData.Agora); // já processado

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_dup_dispute", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeCreatedPayload("pi_dup_dispute"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.EmDisputa);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_PagamentoPendente_LogWarnEnoOp()
    {
        // Estado inconsistente: disputa de Pagamento Pendente (não há cobrança capturada).
        // Defensivo: warn + no-op — não estourar exception (Stripe retentaria infinitamente).
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_pend_dispute", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_pend_dispute", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeCreatedPayload("pi_pend_dispute"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pendente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_PagamentoNaoEncontrado_NaoComita()
    {
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_ghost_dispute", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeCreatedPayload("pi_ghost_dispute"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_SemPaymentIntent_NaoComita()
    {
        const string payload = "{\"type\":\"charge.dispute.created\",\"data\":{\"object\":{\"id\":\"dp_x\",\"reason\":\"fraudulent\"}}}";

        var result = await _handler.HandleAsync(new ProcessarWebhookStripeCommand(payload, ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_AssinaturaNaoEncontrada_MarcaEmDisputaSemForcarInadimplente()
    {
        // Pagamento órfão: marca em disputa pra preservar contabilidade, mas sem assinatura
        // pra congelar — log + commit do Pagamento ainda acontece.
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_orphan_dispute", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.ClearDomainEvents();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_orphan_dispute", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeCreatedPayload("pi_orphan_dispute"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.EmDisputa);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntent_ConnectAccount_AssinaturaNaoEncontrada_Rejeita()
    {
        // ValidarConnectAccountAsync: assinatura is null → false (rejeita, não muta).
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_sem_assin", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_sem_assin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        var result = await _handler.HandleAsync(new ProcessarWebhookStripeCommand(
            PaymentIntentPayloadComConnectAccount("payment_intent.succeeded", "pi_sem_assin", "acct_any"),
            ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pendente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntent_ConnectAccount_TreinadorSemConnectAccount_Rejeita()
    {
        // ValidarConnectAccountAsync: conta?.StripeConnectAccountId is null → false (warn + rejeita).
        var treinadorId = Guid.NewGuid();
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_no_connect", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m, DateTime.UtcNow).Value;

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_no_connect", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        // conta sem ConfigurarStripeConnect → StripeConnectAccountId null (ou conta inexistente)
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContaRecebimento?)null);

        var result = await _handler.HandleAsync(new ProcessarWebhookStripeCommand(
            PaymentIntentPayloadComConnectAccount("payment_intent.succeeded", "pi_no_connect", "acct_any"),
            ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pendente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AccountUpdated_ContaNaoEncontrada_NaoComita()
    {
        // ProcessarContaAtualizadaAsync: contaRecebimento is null → JaConsistente, sem commit.
        _contaRecebimentoRepo.Setup(r => r.ObterPorStripeAccountIdAsync("acct_unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContaRecebimento?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(AccountPayload("acct_unknown", true), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AccountUpdated_OnboardingJaCompleto_Idempotente_NaoComita()
    {
        // ProcessarContaAtualizadaAsync: OnboardingCompleto já true → JaConsistente, sem commit.
        var contaRecebimento = ContaRecebimento.Criar(Guid.NewGuid(), DateTime.UtcNow).Value;
        contaRecebimento.ConfigurarStripeConnect("acct_done", TestData.Agora);
        contaRecebimento.ConfirmarOnboarding(TestData.Agora); // já completo
        _contaRecebimentoRepo.Setup(r => r.ObterPorStripeAccountIdAsync("acct_done", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(AccountPayload("acct_done", true), ValidSig));

        result.IsSuccess.Should().BeTrue();
        contaRecebimento.OnboardingCompleto.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntent_ConnectAccountDivergente_RejeitaSemMutar()
    {
        // Cross-account replay: PaymentIntent existe mas pertence a outro Connect account
        var treinadorId = Guid.NewGuid();
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_spoofed", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        var conta = ContaRecebimento.Criar(treinadorId, DateTime.UtcNow).Value;
        conta.ConfigurarStripeConnect("acct_correct", TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_spoofed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        var result = await _handler.HandleAsync(new ProcessarWebhookStripeCommand(
            PaymentIntentPayloadComConnectAccount("payment_intent.succeeded", "pi_spoofed", "acct_attacker"),
            ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pendente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
