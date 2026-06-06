using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Application.Settings;
using forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
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

        var criarPagamentoService = new CriarPagamentoComIntentService(
            _unitOfWork.Object, _transactionProvider.Object, TimeProvider.System,
            Mock.Of<ILogger<CriarPagamentoComIntentService>>());

        _handler = new GerarCobrancaMensalHandler(
            _assinaturaRepo.Object, _pagamentoRepo.Object, _contaRecebimentoRepo.Object,
            _stripeService.Object, criarPagamentoService,
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
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);
        var zumbi = Pagamento.Criar(assinatura.Id, assinatura.Valor, DateTime.UtcNow).Value;

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
    public async Task HandleAsync_TreinadorSemOnboarding_RetornaFailureStripe()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var assinatura = CriarAssinaturaAluno(treinador.Id);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContaRecebimento?)null);

        var result = await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("treinador_sem_conta_stripe");
    }

    [Fact]
    public async Task HandleAsync_AssinaturaAlunoNaoEncontrada_RetornaFailureNotFound()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        var result = await _handler.HandleAsync(new GerarCobrancaMensalCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("assinatura_aluno_nao_encontrada");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

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

    [Fact]
    public async Task HandleAsync_HappyPath_PersistePagamentoComIntentId()
    {
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

        pagamentoAdicionado.Should().NotBeNull();
        pagamentoAdicionado!.StripePaymentIntentId.Should().NotBeNullOrEmpty(
            because: "o Pagamento só deve ser persistido após o Stripe retornar o intent id (G-PAY-1)");
        pagamentoAdicionado.Status.Should().Be(PagamentoStatus.Pendente);

        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Idempotencia_StripeRecebeIdempotencyKeyDerivadaDoPagamentoId()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinador.Id));

        var fakeStripe = new FakeStripeService();
        var criarPagamentoService = new CriarPagamentoComIntentService(
            _unitOfWork.Object, _transactionProvider.Object, TimeProvider.System,
            Mock.Of<ILogger<CriarPagamentoComIntentService>>());
        var handler = new GerarCobrancaMensalHandler(
            _assinaturaRepo.Object, _pagamentoRepo.Object, _contaRecebimentoRepo.Object,
            fakeStripe, criarPagamentoService,
            Options.Create(new PaymentSettings { TaxaPlataformaPercent = 5m }),
            TimeProvider.System, _logger.Object);

        Pagamento? pagamentoAdicionado = null;
        _pagamentoRepo.Setup(r => r.AdicionarAsync(It.IsAny<Pagamento>(), It.IsAny<CancellationToken>()))
            .Callback<Pagamento, CancellationToken>((p, _) => pagamentoAdicionado = p);

        var result = await handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));

        result.IsSuccess.Should().BeTrue();
        pagamentoAdicionado.Should().NotBeNull();

        var expectedIntentPrefix = $"pi_fake_{pagamentoAdicionado!.Id:N}";
        pagamentoAdicionado.StripePaymentIntentId.Should().Be(expectedIntentPrefix,
            because: "o StripePaymentIntentId deve ser derivado do Pagamento.Id para garantir idempotência em retries");
    }

    [Fact]
    public async Task HandleAsync_FalhaNoCommit_StripeJaFoiChamado_ExcecaoPropagaSemZumbi()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinaturaAluno(treinador.Id);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinador.Id));

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

        stripeCalledWithPagamentoId.Should().NotBeNull();

        _pagamentoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Pagamento>(), It.IsAny<CancellationToken>()), Times.Once,
            "AdicionarAsync foi chamado, mas CommitAsync lançou — o Pagamento nunca foi gravado");
    }
}
