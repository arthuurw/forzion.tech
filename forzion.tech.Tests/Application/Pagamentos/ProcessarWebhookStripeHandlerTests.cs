using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Pagamentos;

public class ProcessarWebhookStripeHandlerTests
{
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IContaRecebimentoRepository> _contaRecebimentoRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<ProcessarWebhookStripeHandler>> _logger = new();
    private readonly ProcessarWebhookStripeHandler _handler;

    private const string ValidSig = "t=1,v1=abc";

    public ProcessarWebhookStripeHandlerTests()
    {
        _handler = new ProcessarWebhookStripeHandler(
            _pagamentoRepo.Object, _assinaturaRepo.Object, _contaRecebimentoRepo.Object,
            _stripeService.Object, _unitOfWork.Object, TimeProvider.System, _logger.Object);

        _stripeService.Setup(s => s.ValidarWebhookAsync(It.IsAny<string>(), ValidSig))
            .ReturnsAsync(true);
    }

    private static string PaymentIntentPayload(string type, string paymentIntentId) =>
        "{\"type\":\"" + type + "\",\"data\":{\"object\":{\"id\":\"" + paymentIntentId + "\"}}}";

    private static string AccountPayload(string accountId, bool chargesEnabled) =>
        "{\"type\":\"account.updated\",\"account\":\"" + accountId + "\",\"data\":{\"object\":{\"charges_enabled\":" + (chargesEnabled ? "true" : "false") + "}}}";

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
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow);
        pagamento.DefinirDadosPix("pi_abc", "qr", "url", DateTime.UtcNow.AddHours(1));
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, DateTime.UtcNow);

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
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 150m, DateTime.UtcNow);
        pagamento.DefinirDadosPix("pi_dup", "qr", "url", DateTime.UtcNow.AddHours(1));
        pagamento.MarcarPago(); // já processado

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
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 150m, DateTime.UtcNow);
        pagamento.DefinirDadosPix("pi_f2", "qr", "url", DateTime.UtcNow.AddHours(1));
        pagamento.MarcarFalhou(); // já processado

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
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 150m, DateTime.UtcNow);
        pagamento.DefinirDadosPix("pi_e2", "qr", "url", DateTime.UtcNow.AddHours(1));
        pagamento.MarcarExpirado(); // já processado

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
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow);
        pagamento.DefinirDadosPix("pi_orphan", "qr", "url", DateTime.UtcNow.AddHours(1));

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
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 150m, DateTime.UtcNow);
        pagamento.DefinirDadosPix("pi_fail", "qr", "url", DateTime.UtcNow.AddHours(1));
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_fail", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.payment_failed", "pi_fail"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Falhou);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentCanceled_MarcaExpirado()
    {
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 150m, DateTime.UtcNow);
        pagamento.DefinirDadosPix("pi_can", "qr", "url", DateTime.UtcNow.AddHours(1));
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
        var contaRecebimento = ContaRecebimento.Criar(Guid.NewGuid(), DateTime.UtcNow);
        contaRecebimento.ConfigurarStripeConnect("acct_ok");
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
}
