using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Tests.Builders;
using forzion.tech.Tests.E2E;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Application.Pagamentos;

public class GerarCobrancaMensalHandlerTests
{
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IContaRecebimentoRepository> _contaRecebimentoRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDbContextTransactionProvider> _transactionProvider = new();
    private readonly Mock<ILogger<GerarCobrancaMensalHandler>> _logger = new();
    private readonly GerarCobrancaMensalHandler _handler;

    private sealed class NoopTransaction : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static readonly PixPaymentResult PixResult = new("pi_123", "qrcode", "https://img", DateTime.UtcNow.AddHours(1));

    public GerarCobrancaMensalHandlerTests()
    {
        var paymentSettings = Options.Create(new PaymentSettings { TaxaPlataformaPercent = 5m });

        _transactionProvider.Setup(p => p.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NoopTransaction());

        _handler = new GerarCobrancaMensalHandler(
            _assinaturaRepo.Object, _pagamentoRepo.Object, _contaRecebimentoRepo.Object,
            _stripeService.Object, _unitOfWork.Object, _transactionProvider.Object,
            paymentSettings, TimeProvider.System, _logger.Object);

        _stripeService.Setup(s => s.CriarPixPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PixResult);
    }

    private static AssinaturaAluno CriarAssinaturaAluno(Guid treinadorId)
    {
        var a = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        return a;
    }

    private static Treinador CriarTreinadorComOnboarding() => Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;

    private static ContaRecebimento ContaOnboarded(Guid treinadorId)
    {
        var c = ContaRecebimento.Criar(treinadorId, DateTime.UtcNow).Value;
        c.ConfigurarStripeConnect("acct_123", TestData.Agora);
        c.ConfirmarOnboarding(TestData.Agora);
        return c;
    }

    [Fact]
    public async Task HandleAsync_AssinaturaAlunoValida_GeraPixERetornaResponse()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinador.Id));

        var result = await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.PixQrCode.Should().Be("qrcode");
        result.Value.PixQrCodeUrl.Should().Be("https://img");
        result.Value.MetodoPagamento.Should().Be(MetodoPagamento.Pix);
    }

    [Fact]
    public async Task HandleAsync_TreinadorErrado_LancaAcessoNegado()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var act = async () => await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<forzion.tech.Domain.Exceptions.AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_PagamentoPendenteExistente_RetornaExistente()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);
        var pagamentoPendente = Pagamento.Criar(assinatura.Id, assinatura.Valor, DateTime.UtcNow).Value;
        pagamentoPendente.DefinirDadosPix("pi_old", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamentoPendente);
        // Conta Stripe verificada antes da tx (early-exit para treinador sem onboarding)
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinador.Id));

        var result = await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.PagamentoId.Should().Be(pagamentoPendente.Id);
        _stripeService.Verify(s => s.CriarPixPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PagamentoZumbi_MarcaFalhouECriaNovoCharge()
    {
        // Zumbi: Pendente sem StripePaymentIntentId — Stripe falhou em tentativa anterior
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);
        var zumbi = Pagamento.Criar(assinatura.Id, assinatura.Valor, DateTime.UtcNow).Value; // sem DefinirDadosPix

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(zumbi);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinador.Id));

        var result = await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));

        result.IsSuccess.Should().BeTrue();
        zumbi.Status.Should().Be(PagamentoStatus.Falhou);
        result.Value.PixQrCode.Should().Be("qrcode");
        _stripeService.Verify(s => s.CriarPixPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_StripeFalha_PropagaExcecaoSemPersistirPagamento()
    {
        // G-PAY-1: Stripe é chamado ANTES do commit. Se falhar, nenhum Pagamento é
        // gravado no banco (rollback implícito) → sem zumbi, sem slot ocupado.
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinador.Id));
        _stripeService.Setup(s => s.CriarPixPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Stripe indisponível"));

        var act = async () => await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));
        await act.Should().ThrowAsync<InvalidOperationException>();

        // AdicionarAsync nunca foi chamado — Stripe falhou antes do commit
        _pagamentoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Pagamento>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaAlunoCancelada_RetornaFailure()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);
        assinatura.Cancelar(DateTime.UtcNow);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_TreinadorSemOnboarding_LancaDomainException()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var assinatura = CriarAssinaturaAluno(treinador.Id);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContaRecebimento?)null);

        var act = async () => await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*Stripe*");
    }

    [Fact]
    public async Task HandleAsync_AssinaturaAlunoNaoEncontrada_LancaDomainException()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        var act = async () => await _handler.HandleAsync(new GerarCobrancaMensalCommand(Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>().WithMessage("AssinaturaAluno não encontrada.");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // --- Método Cartão ---

    private static readonly CartaoPaymentResult CartaoResult = new("pi_cartao_123", "secret_abc");

    [Fact]
    public async Task HandleAsync_MetodoCartao_GeraCartaoERetornaResponse()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinador.Id));
        _stripeService.Setup(s => s.CriarCartaoPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartaoResult);

        var result = await _handler.HandleAsync(
            new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id, MetodoPagamento.Cartao));

        result.IsSuccess.Should().BeTrue();
        result.Value.MetodoPagamento.Should().Be(MetodoPagamento.Cartao);
        _stripeService.Verify(s => s.CriarCartaoPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MetodoCartao_StripeFalha_PropagaExcecaoSemPersistirPagamento()
    {
        // G-PAY-1: Stripe Cartão é chamado ANTES do commit. Se falhar, nenhum Pagamento é
        // gravado no banco (rollback implícito) → sem zumbi, sem slot ocupado.
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinador.Id));
        _stripeService.Setup(s => s.CriarCartaoPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Stripe cartão indisponível"));

        var act = async () => await _handler.HandleAsync(
            new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id, MetodoPagamento.Cartao));
        await act.Should().ThrowAsync<InvalidOperationException>();

        // AdicionarAsync nunca foi chamado — Stripe falhou antes do commit
        _pagamentoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Pagamento>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_MetodoCartao_NaoChama_CriarPixPaymentIntent()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinador.Id));
        _stripeService.Setup(s => s.CriarCartaoPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartaoResult);

        await _handler.HandleAsync(
            new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id, MetodoPagamento.Cartao));

        _stripeService.Verify(s => s.CriarPixPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── G-PAY-1: testes de atomicidade ───────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_HappyPath_PersistePagamentoComIntentId()
    {
        // (a) Happy path: único CommitAsync persiste Pagamento já com StripePaymentIntentId.
        // Garante que a janela de zumbi foi eliminada: nenhum commit acontece antes de
        // termos o intent id do Stripe.
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinador.Id));

        Pagamento? pagamentoAdicionado = null;
        _pagamentoRepo.Setup(r => r.AdicionarAsync(It.IsAny<Pagamento>(), It.IsAny<CancellationToken>()))
            .Callback<Pagamento, CancellationToken>((p, _) => pagamentoAdicionado = p);

        var result = await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));

        result.IsSuccess.Should().BeTrue();

        // Pagamento foi entregue ao repositório já com StripePaymentIntentId preenchido
        pagamentoAdicionado.Should().NotBeNull();
        pagamentoAdicionado!.StripePaymentIntentId.Should().NotBeNullOrEmpty(
            because: "o Pagamento só deve ser persistido após o Stripe retornar o intent id (G-PAY-1)");
        pagamentoAdicionado.Status.Should().Be(PagamentoStatus.Pendente);

        // Apenas um CommitAsync — sem janela de dois commits
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Idempotencia_StripeRecebeIdempotencyKeyDerivadaDoPagamentoId()
    {
        // (b) Idempotency: FakeStripeService (espelhando o comportamento do StripeService real)
        // deriva o PaymentIntentId do pagamentoId. Verificamos que o intent id retornado
        // pelo Stripe é construído com o mesmo Guid do Pagamento — provando que retries
        // com o mesmo Pagamento.Id reutilizariam o mesmo intent (no StripeService real,
        // via IdempotencyKey = "pagamento-{guid:N}").
        //
        // O FakeStripeService usa a convenção: PaymentIntentId = $"pi_fake_{pagamentoId:N}".
        // Ao inspecionar o StripePaymentIntentId persistido, confirmamos que ele foi
        // construído com o Guid correto — qualquer retry com o mesmo Guid (mesma
        // idempotency key) receberia o mesmo intent do Stripe real.
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinador.Id));

        // Use FakeStripeService directly to mirror E2E pipeline behavior
        var fakeStripe = new FakeStripeService();
        var handler = new GerarCobrancaMensalHandler(
            _assinaturaRepo.Object, _pagamentoRepo.Object, _contaRecebimentoRepo.Object,
            fakeStripe, _unitOfWork.Object, _transactionProvider.Object,
            Options.Create(new PaymentSettings { TaxaPlataformaPercent = 5m }),
            TimeProvider.System, _logger.Object);

        Pagamento? pagamentoAdicionado = null;
        _pagamentoRepo.Setup(r => r.AdicionarAsync(It.IsAny<Pagamento>(), It.IsAny<CancellationToken>()))
            .Callback<Pagamento, CancellationToken>((p, _) => pagamentoAdicionado = p);

        var result = await handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));

        result.IsSuccess.Should().BeTrue();
        pagamentoAdicionado.Should().NotBeNull();

        // FakeStripeService: PaymentIntentId = $"pi_fake_{pagamentoId:N}"
        // O intent id deve conter o Guid do Pagamento que foi passado ao Stripe.
        // Isso confirma que o mesmo Pagamento.Id → mesma idempotency key → mesmo intent
        // no StripeService real (idempotent retry seguro).
        var expectedIntentPrefix = $"pi_fake_{pagamentoAdicionado!.Id:N}";
        pagamentoAdicionado.StripePaymentIntentId.Should().Be(expectedIntentPrefix,
            because: "o StripePaymentIntentId deve ser derivado do Pagamento.Id para garantir idempotência em retries");
    }

    [Fact]
    public async Task HandleAsync_FalhaNoCommit_StripeJaFoiChamado_ExcecaoPropagaSemZumbi()
    {
        // (b) Retry safety: se o único CommitAsync falhar depois que Stripe já retornou,
        // a exceção propaga (caller/job pode fazer retry). O Pagamento nunca foi gravado,
        // então não existe zumbi no banco. Um retry chamaria Stripe com um NOVO Guid
        // (Pagamento.Criar gera novo Id) — no StripeService real, nova idempotency key →
        // novo intent. O intent anterior fica órfão e expira (sem captura de dinheiro).
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinador.Id));

        // Stripe succeeds (intent returned)
        // CommitAsync throws — simulates DB failure after Stripe call
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB commit failure"));

        Guid? stripeCalledWithPagamentoId = null;
        _stripeService.Setup(s => s.CriarPixPaymentIntentAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Callback<decimal, string, Guid, decimal, CancellationToken>((_, _, id, _, _) => stripeCalledWithPagamentoId = id)
            .ReturnsAsync(PixResult);

        var act = async () => await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB commit failure");

        // Stripe foi chamado antes do commit (design correto do G-PAY-1)
        stripeCalledWithPagamentoId.Should().NotBeNull();

        // Pagamento foi passado ao repo, mas o commit nunca completou → sem row no banco
        // (NoopTransaction não valida, mas o contrato é: se CommitAsync joga, o caller retenta)
        _pagamentoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Pagamento>(), It.IsAny<CancellationToken>()), Times.Once,
            "AdicionarAsync foi chamado, mas CommitAsync lançou — o Pagamento nunca foi gravado");
    }
}
