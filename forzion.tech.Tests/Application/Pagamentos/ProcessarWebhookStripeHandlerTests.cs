using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.ValueObjects;
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
    private readonly Mock<IAssinaturaTreinadorRepository> _assinaturaTreinadorRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IOutboxEnfileirador> _enfileirador = new();
    private readonly Mock<ILogger<ProcessarWebhookStripeHandler>> _logger = new();
    private readonly ProcessarWebhookStripeHandler _handler;

    private const string ValidSig = "t=1,v1=abc";

    public ProcessarWebhookStripeHandlerTests()
    {
        _handler = new ProcessarWebhookStripeHandler(
            _pagamentoRepo.Object, _assinaturaRepo.Object, _contaRecebimentoRepo.Object,
            _pagamentoTreinadorRepo.Object, _assinaturaTreinadorRepo.Object, _treinadorRepo.Object,
            _alunoRepo.Object, _contaRepo.Object,
            _stripeService.Object, _unitOfWork.Object, _enfileirador.Object, TimeProvider.System, _logger.Object);

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
    public async Task HandleAsync_PaymentIntentSucceeded_PlanoTreinadorCadastro_FinalizaCadastroAtomico()
    {
        var conta = Conta.Criar(Email.Criar("t@x.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow, emitirRegistro: false).Value;
        var planoId = Guid.NewGuid();
        var treinador = Treinador.Criar(conta.Id, "Carlos", DateTime.UtcNow, null, planoId, ModoPagamentoAluno.Plataforma, aguardandoPagamento: true).Value;
        var assinatura = AssinaturaTreinador.Criar(treinador.Id, planoId, 50m, DateTime.UtcNow).Value;
        var pagamento = PagamentoTreinador.Criar(treinador.Id, assinatura.Id, 50m, FinalidadePagamentoTreinador.Cadastro, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_treinador", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_treinador", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaTreinadorRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentTreinadorPayload("payment_intent.succeeded", "pi_treinador"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pago);
        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Ativa);
        assinatura.DataProximaCobranca.Should().BeAfter(assinatura.DataInicio, "renovação agendada para o próximo ciclo");
        treinador.Status.Should().Be(TreinadorStatus.AguardandoAprovacao);
        conta.DomainEvents.OfType<ContaRegistradaEvent>().Should().ContainSingle("verificação só após o pagamento");
        _pagamentoRepo.Verify(r => r.ObterPorPaymentIntentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once, "tudo num único commit (atômico)");
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
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_dup", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);

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
        pagamento.MarcarFalhou(TestData.Agora);

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
        pagamento.MarcarExpirado(TestData.Agora);

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
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentFailed_AssinaturaInadimplenteQuartaFalha_MantemInadimplente()
    {
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_4th_fail", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        assinatura.Ativar(TestData.Agora);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
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
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_AssinaturaInadimplente_Regulariza_VoltaParaAtiva()
    {
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_regul", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        assinatura.Ativar(TestData.Agora);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
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
    public async Task HandleAsync_AccountUpdatedChargesFalse_OnboardingCompleto_LogCritical()
    {
        var contaRecebimento = ContaRecebimento.Criar(Guid.NewGuid(), DateTime.UtcNow).Value;
        contaRecebimento.ConfigurarStripeConnect("acct_live", TestData.Agora);
        contaRecebimento.ConfirmarOnboarding(TestData.Agora);
        _contaRecebimentoRepo.Setup(r => r.ObterPorStripeAccountIdAsync("acct_live", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(AccountPayload("acct_live", false), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _logger.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AccountUpdatedChargesFalse_OnboardingIncompleto_SemCritical()
    {
        var contaRecebimento = ContaRecebimento.Criar(Guid.NewGuid(), DateTime.UtcNow).Value;
        contaRecebimento.ConfigurarStripeConnect("acct_onboarding", TestData.Agora);
        _contaRecebimentoRepo.Setup(r => r.ObterPorStripeAccountIdAsync("acct_onboarding", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(AccountPayload("acct_onboarding", false), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _logger.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AccountUpdatedChargesFalse_ContaInexistente_SemCritical()
    {
        _contaRecebimentoRepo.Setup(r => r.ObterPorStripeAccountIdAsync("acct_ghost", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContaRecebimento?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(AccountPayload("acct_ghost", false), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _logger.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
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
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_dup_refund", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.MarcarEstornado(TestData.Agora);

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
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_partial_refund", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.ClearDomainEvents();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_partial_refund", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

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
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_full_refund", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.ClearDomainEvents();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_full_refund", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeRefundedPayload("pi_full_refund", 14990), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Estornado);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChargeRefunded_AmountRefundedAusente_MarcaEstornado()
    {
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
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_dup_dispute", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.MarcarEmDisputa("fraudulent", TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_dup_dispute", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeCreatedPayload("pi_dup_dispute"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.EmDisputa);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_Aluno_JaEmDisputa_NaoReenviaNemComita()
    {
        var assinaturaId = Guid.NewGuid();
        var inicio = DateTime.UtcNow.AddDays(-10);
        var pagamento = Pagamento.Criar(assinaturaId, 149.90m, inicio).Value;
        pagamento.DefinirDadosPix("pi_redeliver", "qr", "url", inicio.AddHours(1), inicio);
        pagamento.MarcarPago(inicio);
        pagamento.MarcarEmDisputa("fraudulent", inicio);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_redeliver", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeCreatedPayload("pi_redeliver"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _enfileirador.Verify(e => e.Enfileirar(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()), Times.Never);
        _stripeService.Verify(s => s.EnviarEvidenciaDisputaAsync(It.IsAny<string>(), It.IsAny<DisputaEvidencia>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_PagamentoPendente_LogWarnEnoOp()
    {
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
    public async Task HandleAsync_ChargeDisputeCreated_Aluno_EnfileiraPayloadComCamposD9()
    {
        var alunoId = Guid.NewGuid();
        var inicio = DateTime.UtcNow.AddDays(-30);
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 149.90m, inicio).Value;
        pagamento.DefinirDadosPix("pi_ev", "qr", "url", inicio.AddHours(1), inicio);
        pagamento.MarcarPago(inicio);
        pagamento.ClearDomainEvents();

        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), alunoId, 149.90m, inicio).Value;
        assinatura.Ativar(inicio);
        assinatura.ClearDomainEvents();

        var conta = Conta.Criar(Email.Criar("aluno@x.com").Value, "hash", TipoConta.Aluno, inicio, emitirRegistro: false).Value;
        var aluno = Aluno.Criar(conta.Id, "Joana", inicio, "aluno@x.com").Value;

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_ev", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeCreatedPayload("pi_ev"), ValidSig));

        _enfileirador.Verify(e => e.Enfileirar(
            "fx:evidencia_disputa",
            It.Is<forzion.tech.Application.Outbox.EvidenciaDisputaPayload>(p =>
                p.DisputeId == "dp_x" &&
                p.Email == "aluno@x.com" &&
                p.DataAtivacao == assinatura.DataInicio &&
                p.DataPagamento == pagamento.DataPagamento &&
                p.PagamentoId == pagamento.Id),
            $"fx:evidencia_disputa:aluno:{pagamento.Id}"), Times.Once);
        _stripeService.Verify(s => s.EnviarEvidenciaDisputaAsync(It.IsAny<string>(), It.IsAny<DisputaEvidencia>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_Aluno_SemAssinatura_EnfileiraPayloadSemEmail()
    {
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_ev_no_assin", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.ClearDomainEvents();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_ev_no_assin", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((AssinaturaAluno?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeCreatedPayload("pi_ev_no_assin"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.EmDisputa);
        // Sem assinatura: enfileira com email null (sem acesso a aluno/conta).
        _enfileirador.Verify(e => e.Enfileirar(
            "fx:evidencia_disputa",
            It.Is<forzion.tech.Application.Outbox.EvidenciaDisputaPayload>(p => p.Email == null && p.PagamentoId == pagamento.Id),
            $"fx:evidencia_disputa:aluno:{pagamento.Id}"), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_Treinador_EnfileiraPayloadComEmail()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = AssinaturaTreinador.Criar(treinadorId, Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);
        assinatura.ClearDomainEvents();
        var pagamento = PagamentoTreinador.Criar(treinadorId, assinatura.Id, 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t_ev", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);
        pagamento.MarcarPago(DateTime.UtcNow);
        pagamento.ClearDomainEvents();

        var conta = Conta.Criar(Email.Criar("treinador@x.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow, emitirRegistro: false).Value;
        var treinador = Treinador.Criar(conta.Id, "Carlos", DateTime.UtcNow, null, Guid.NewGuid(), ModoPagamentoAluno.Plataforma).Value;

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_t_ev", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaTreinadorRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_t_ev", It.IsAny<CancellationToken>())).ReturnsAsync((Pagamento?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeTreinadorPayload("pi_t_ev"), ValidSig));

        _enfileirador.Verify(e => e.Enfileirar(
            "fx:evidencia_disputa",
            It.Is<forzion.tech.Application.Outbox.EvidenciaDisputaPayload>(p =>
                p.DisputeId == "dp_t" &&
                p.Email == "treinador@x.com" &&
                p.PagamentoId == pagamento.Id),
            $"fx:evidencia_disputa:treinador:{pagamento.Id}"), Times.Once);
        _stripeService.Verify(s => s.EnviarEvidenciaDisputaAsync(It.IsAny<string>(), It.IsAny<DisputaEvidencia>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntent_ConnectAccount_AssinaturaNaoEncontrada_Rejeita()
    {
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
        var treinadorId = Guid.NewGuid();
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_no_connect", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m, DateTime.UtcNow).Value;

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_no_connect", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
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
        var contaRecebimento = ContaRecebimento.Criar(Guid.NewGuid(), DateTime.UtcNow).Value;
        contaRecebimento.ConfigurarStripeConnect("acct_done", TestData.Agora);
        contaRecebimento.ConfirmarOnboarding(TestData.Agora);
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

    [Fact]
    public async Task HandleAsync_PaymentFailed_PlanoTreinador_MarcaFalhouERegistraFalha()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = AssinaturaTreinador.Criar(treinadorId, Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);
        var pagamento = PagamentoTreinador.Criar(treinadorId, assinatura.Id, 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_treinador_fail", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_treinador_fail", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaTreinadorRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentTreinadorPayload("payment_intent.payment_failed", "pi_treinador_fail"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Falhou);
        assinatura.TentativasFalhasConsecutivas.Should().Be(1);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentFailed_PlanoTreinador_TerceiraFalha_MarcaInadimplente()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = AssinaturaTreinador.Criar(treinadorId, Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
        assinatura.RegistrarPagamentoFalho(DateTime.UtcNow);
        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Ativa, "2 falhas ainda não tornam inadimplente");

        var pagamento = PagamentoTreinador.Criar(treinadorId, assinatura.Id, 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_treinador_fail3", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_treinador_fail3", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaTreinadorRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentTreinadorPayload("payment_intent.payment_failed", "pi_treinador_fail3"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        assinatura.TentativasFalhasConsecutivas.Should().Be(3);
        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Inadimplente, "3ª falha deve tornar inadimplente");
    }

    [Fact]
    public async Task HandleAsync_PaymentFailed_PlanoTreinador_PagamentoNaoEncontrado_JaConsistente()
    {
        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentTreinadorPayload("payment_intent.payment_failed", "pi_nao_existe"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentFailed_PlanoTreinador_PagamentoJaFalhou_Idempotente()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = AssinaturaTreinador.Criar(treinadorId, Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);
        var pagamento = PagamentoTreinador.Criar(treinadorId, assinatura.Id, 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_ja_falhou", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);
        pagamento.MarcarFalhou(DateTime.UtcNow);

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_ja_falhou", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentTreinadorPayload("payment_intent.payment_failed", "pi_ja_falhou"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PlanoTreinadorCadastro_AssinaturaCancelada_NaoComita()
    {
        var conta = Conta.Criar(Email.Criar("t@x.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow, emitirRegistro: false).Value;
        var planoId = Guid.NewGuid();
        var treinador = Treinador.Criar(conta.Id, "Carlos", DateTime.UtcNow, null, planoId, ModoPagamentoAluno.Plataforma, aguardandoPagamento: true).Value;
        var assinatura = AssinaturaTreinador.Criar(treinador.Id, planoId, 50m, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);
        assinatura.Cancelar(DateTime.UtcNow);
        var pagamento = PagamentoTreinador.Criar(treinador.Id, assinatura.Id, 50m, FinalidadePagamentoTreinador.Cadastro, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t1_fail", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_t1_fail", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaTreinadorRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        var act = async () => await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentTreinadorPayload("payment_intent.succeeded", "pi_t1_fail"), ValidSig));

        await act.Should().ThrowAsync<Exception>("falha em FinalizarCadastroAsync deve impedir commit parcial");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PlanoTreinadorCadastro_TreinadorNaoAguardandoPagamento_NaoComita()
    {
        var conta = Conta.Criar(Email.Criar("t2@x.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow, emitirRegistro: false).Value;
        var planoId = Guid.NewGuid();
        var treinador = Treinador.Criar(conta.Id, "Ana", DateTime.UtcNow, null, planoId, ModoPagamentoAluno.Plataforma, aguardandoPagamento: false).Value;
        var assinatura = AssinaturaTreinador.Criar(treinador.Id, planoId, 50m, DateTime.UtcNow).Value;
        var pagamento = PagamentoTreinador.Criar(treinador.Id, assinatura.Id, 50m, FinalidadePagamentoTreinador.Cadastro, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t1_treinador_fail", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_t1_treinador_fail", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaTreinadorRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        var act = async () => await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentTreinadorPayload("payment_intent.succeeded", "pi_t1_treinador_fail"), ValidSig));

        await act.Should().ThrowAsync<Exception>("falha em ConfirmarPagamentoPlano deve impedir commit");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ConnectNull_TreinadorSemStripeAccount_LancaExcecao()
    {
        var treinadorId = Guid.NewGuid();
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_null_connect", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        var contaRecebimento = ContaRecebimento.Criar(treinadorId, DateTime.UtcNow).Value;

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_null_connect", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);

        var act = async () => await _handler.HandleAsync(new ProcessarWebhookStripeCommand(
            PaymentIntentPayloadComConnectAccount("payment_intent.succeeded", "pi_null_connect", "acct_any"),
            ValidSig));

        await act.Should().ThrowAsync<Exception>("connect-null é erro de configuração, não rejeição legítima");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ConnectMismatch_ContinuaRetornandoJaConsistente()
    {
        var treinadorId = Guid.NewGuid();
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t2_mismatch", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        var contaRecebimento = ContaRecebimento.Criar(treinadorId, DateTime.UtcNow).Value;
        contaRecebimento.ConfigurarStripeConnect("acct_correct", TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_t2_mismatch", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);

        var result = await _handler.HandleAsync(new ProcessarWebhookStripeCommand(
            PaymentIntentPayloadComConnectAccount("payment_intent.succeeded", "pi_t2_mismatch", "acct_attacker"),
            ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pendente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_ComConnect_AssinaturaLidaApenas1Vez()
    {
        var treinadorId = Guid.NewGuid();
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t3_ok", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        var contaRecebimento = ContaRecebimento.Criar(treinadorId, DateTime.UtcNow).Value;
        contaRecebimento.ConfigurarStripeConnect("acct_t3", TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_t3_ok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);

        var result = await _handler.HandleAsync(new ProcessarWebhookStripeCommand(
            PaymentIntentPayloadComConnectAccount("payment_intent.succeeded", "pi_t3_ok", "acct_t3"),
            ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Pago);
        _assinaturaRepo.Verify(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentFailed_ComConnect_AssinaturaLidaApenas1Vez()
    {
        var treinadorId = Guid.NewGuid();
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t3_fail", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        var contaRecebimento = ContaRecebimento.Criar(treinadorId, DateTime.UtcNow).Value;
        contaRecebimento.ConfigurarStripeConnect("acct_t3f", TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_t3_fail", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);

        await _handler.HandleAsync(new ProcessarWebhookStripeCommand(
            PaymentIntentPayloadComConnectAccount("payment_intent.payment_failed", "pi_t3_fail", "acct_t3f"),
            ValidSig));

        _assinaturaRepo.Verify(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static string ChargeRefundedTreinadorPayload(string paymentIntentId, long amountCents = 5000) =>
        "{\"type\":\"charge.refunded\",\"data\":{\"object\":{\"id\":\"ch_t\",\"payment_intent\":\"" + paymentIntentId + "\",\"amount_refunded\":" + amountCents + ",\"refunded\":true}}}";

    private static string ChargeDisputeTreinadorPayload(string paymentIntentId, string motivo = "fraudulent") =>
        "{\"type\":\"charge.dispute.created\",\"data\":{\"object\":{\"id\":\"dp_t\",\"payment_intent\":\"" + paymentIntentId + "\",\"reason\":\"" + motivo + "\",\"amount\":5000}}}";

    [Fact]
    public async Task HandleAsync_ChargeRefunded_PagamentoTreinadorPago_MarcaEstornadoECongela()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = AssinaturaTreinador.Criar(treinadorId, Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);
        assinatura.ClearDomainEvents();
        var pagamento = PagamentoTreinador.Criar(treinadorId, assinatura.Id, 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t4_refund", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);
        pagamento.MarcarPago(DateTime.UtcNow);
        pagamento.ClearDomainEvents();

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_t4_refund", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaTreinadorRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_t4_refund", It.IsAny<CancellationToken>())).ReturnsAsync((Pagamento?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeRefundedTreinadorPayload("pi_t4_refund"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Estornado);
        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Inadimplente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChargeRefunded_PagamentoTreinadorJaEstornado_Idempotente()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = AssinaturaTreinador.Criar(treinadorId, Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);
        var pagamento = PagamentoTreinador.Criar(treinadorId, assinatura.Id, 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t4_refund_dup", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);
        pagamento.MarcarPago(DateTime.UtcNow);
        pagamento.MarcarEstornado(DateTime.UtcNow);

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_t4_refund_dup", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_t4_refund_dup", It.IsAny<CancellationToken>())).ReturnsAsync((Pagamento?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeRefundedTreinadorPayload("pi_t4_refund_dup"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_PagamentoTreinadorPago_MarcaEmDisputaECongela()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = AssinaturaTreinador.Criar(treinadorId, Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);
        assinatura.ClearDomainEvents();
        var pagamento = PagamentoTreinador.Criar(treinadorId, assinatura.Id, 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t4_dispute", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);
        pagamento.MarcarPago(DateTime.UtcNow);
        pagamento.ClearDomainEvents();

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_t4_dispute", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaTreinadorRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_t4_dispute", It.IsAny<CancellationToken>())).ReturnsAsync((Pagamento?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeTreinadorPayload("pi_t4_dispute"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.EmDisputa);
        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Inadimplente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_PagamentoTreinadorJaEmDisputa_Idempotente()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = AssinaturaTreinador.Criar(treinadorId, Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);
        var pagamento = PagamentoTreinador.Criar(treinadorId, assinatura.Id, 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t4_dispute_dup", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);
        pagamento.MarcarPago(DateTime.UtcNow);
        pagamento.MarcarEmDisputa(DateTime.UtcNow);

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_t4_dispute_dup", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_t4_dispute_dup", It.IsAny<CancellationToken>())).ReturnsAsync((Pagamento?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeTreinadorPayload("pi_t4_dispute_dup"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_PagamentoTreinadorJaEmDisputa_NaoReenviaNemComita()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = AssinaturaTreinador.Criar(treinadorId, Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);
        var pagamento = PagamentoTreinador.Criar(treinadorId, assinatura.Id, 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t4_redeliver", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);
        pagamento.MarcarPago(DateTime.UtcNow);
        pagamento.MarcarEmDisputa(DateTime.UtcNow);

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_t4_redeliver", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_t4_redeliver", It.IsAny<CancellationToken>())).ReturnsAsync((Pagamento?)null);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeTreinadorPayload("pi_t4_redeliver"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _enfileirador.Verify(e => e.Enfileirar(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()), Times.Never);
        _stripeService.Verify(s => s.EnviarEvidenciaDisputaAsync(It.IsAny<string>(), It.IsAny<DisputaEvidencia>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeRefunded_PathAluno_NaoBuscaPagamentoTreinador()
    {
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t4_aluno_refund", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.ClearDomainEvents();

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_t4_aluno_refund", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeRefundedPayload("pi_t4_aluno_refund"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Estornado);
        _pagamentoTreinadorRepo.Verify(r => r.ObterPorStripePaymentIntentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_AssinaturaCancelada_RefundReverseTransferEMarcaEstornado()
    {
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_x", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        assinatura.Cancelar(TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_x", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.succeeded", "pi_x"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Estornado);
        _stripeService.Verify(s => s.CriarReembolsoAsync("pi_x", true, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_AssinaturaCancelada_Redelivery_NaoRefundaDuasVezes()
    {
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_x", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        pagamento.MarcarPago(TestData.Agora);
        pagamento.MarcarEstornado(TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        assinatura.Cancelar(TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_x", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.succeeded", "pi_x"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        _stripeService.Verify(s => s.CriarReembolsoAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_AssinaturaCancelada_RefundFalha_NaoComita()
    {
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_x", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        assinatura.Cancelar(TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_x", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _stripeService.Setup(s => s.CriarReembolsoAsync("pi_x", true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("stripe down"));

        var act = async () => await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentPayload("payment_intent.succeeded", "pi_x"), ValidSig));

        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
