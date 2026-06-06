using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Application.UseCases.Treinadores.GerarCobrancaPlanoTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.E2E;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class GerarCobrancaPlanoTreinadorHandlerTests
{
    private readonly Mock<IAssinaturaTreinadorRepository> _assinaturaRepo = new();
    private readonly Mock<IPagamentoTreinadorRepository> _pagamentoRepo = new();
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDbContextTransactionProvider> _transactionProvider = new();
    private readonly Mock<ILogger<GerarCobrancaPlanoTreinadorHandler>> _logger = new();
    private readonly GerarCobrancaPlanoTreinadorHandler _handler;

    private sealed class NoopTransaction : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static readonly PixPaymentResult PixResult = new("pi_treinador_123", "qrcode", "https://img", DateTime.UtcNow.AddHours(1));
    private static readonly CartaoPaymentResult CartaoResult = new("pi_treinador_cartao_123", "secret_abc");

    public GerarCobrancaPlanoTreinadorHandlerTests()
    {
        _transactionProvider.Setup(p => p.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NoopTransaction());

        _stripeService.Setup(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PixResult);

        var criarPagamentoService = new CriarPagamentoComIntentService(
            _unitOfWork.Object, _transactionProvider.Object, TimeProvider.System,
            Mock.Of<ILogger<CriarPagamentoComIntentService>>());

        _handler = new GerarCobrancaPlanoTreinadorHandler(
            _assinaturaRepo.Object, _pagamentoRepo.Object, _planoRepo.Object,
            _stripeService.Object, _unitOfWork.Object, criarPagamentoService,
            TimeProvider.System, _logger.Object);
    }

    private static AssinaturaTreinador CriarAssinaturaAtiva()
    {
        var a = AssinaturaTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        a.Ativar(DateTime.UtcNow);
        return a;
    }

    private static AssinaturaTreinador CriarAssinaturaInadimplente()
    {
        var a = AssinaturaTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        a.Ativar(DateTime.UtcNow);
        a.MarcarInadimplente(DateTime.UtcNow);
        return a;
    }

    [Fact]
    public async Task HandleAsync_AssinaturaAtiva_GeraPixERetornaResponse()
    {
        var assinatura = CriarAssinaturaAtiva();
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        var result = await _handler.HandleAsync(new GerarCobrancaPlanoTreinadorCommand(assinatura.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.PixQrCode.Should().Be("qrcode");
        result.Value.MetodoPagamento.Should().Be(MetodoPagamento.Pix);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaInadimplente_PermiteBilling()
    {
        var assinatura = CriarAssinaturaInadimplente();
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        var result = await _handler.HandleAsync(new GerarCobrancaPlanoTreinadorCommand(assinatura.Id));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_AssinaturaCancelada_RetornaFailure()
    {
        var assinatura = AssinaturaTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);
        assinatura.Cancelar(DateTime.UtcNow);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new GerarCobrancaPlanoTreinadorCommand(assinatura.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("assinatura_treinador_cancelada");
    }

    [Fact]
    public async Task HandleAsync_AssinaturaPendente_RetornaFailure()
    {
        var assinatura = AssinaturaTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new GerarCobrancaPlanoTreinadorCommand(assinatura.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("assinatura_treinador_pendente");
    }

    [Fact]
    public async Task HandleAsync_PagamentoPendenteExistente_RetornaExistente()
    {
        var assinatura = CriarAssinaturaAtiva();
        var pagamentoPendente = PagamentoTreinador.Criar(assinatura.TreinadorId, assinatura.Id, 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        pagamentoPendente.DefinirDadosPix("pi_old", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamentoPendente);

        var result = await _handler.HandleAsync(new GerarCobrancaPlanoTreinadorCommand(assinatura.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.PagamentoId.Should().Be(pagamentoPendente.Id);
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PagamentoZumbi_MarcaFalhouECriaNovoCharge()
    {
        var assinatura = CriarAssinaturaAtiva();
        var zumbi = PagamentoTreinador.Criar(assinatura.TreinadorId, assinatura.Id, 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(zumbi);

        var result = await _handler.HandleAsync(new GerarCobrancaPlanoTreinadorCommand(assinatura.Id));

        result.IsSuccess.Should().BeTrue();
        zumbi.Status.Should().Be(PagamentoStatus.Falhou);
        result.Value.PixQrCode.Should().Be("qrcode");
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_StripeFalha_PropagaExcecaoSemPersistir()
    {
        var assinatura = CriarAssinaturaAtiva();
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);
        _stripeService.Setup(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Stripe indisponível"));

        var act = async () => await _handler.HandleAsync(new GerarCobrancaPlanoTreinadorCommand(assinatura.Id));
        await act.Should().ThrowAsync<InvalidOperationException>();

        _pagamentoRepo.Verify(r => r.AdicionarAsync(It.IsAny<PagamentoTreinador>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaNaoEncontrada_RetornaFailureNotFound()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaTreinador?)null);

        var result = await _handler.HandleAsync(new GerarCobrancaPlanoTreinadorCommand(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("assinatura_treinador_nao_encontrada");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_MetodoCartao_UsaCriarCartaoPlataforma()
    {
        var assinatura = CriarAssinaturaAtiva();
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);
        _stripeService.Setup(s => s.CriarCartaoPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartaoResult);

        var result = await _handler.HandleAsync(new GerarCobrancaPlanoTreinadorCommand(assinatura.Id, MetodoPagamento.Cartao));

        result.IsSuccess.Should().BeTrue();
        result.Value.MetodoPagamento.Should().Be(MetodoPagamento.Cartao);
        _stripeService.Verify(s => s.CriarCartaoPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_HappyPath_PersistePagamentoComIntentId()
    {
        var assinatura = CriarAssinaturaAtiva();
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        PagamentoTreinador? pagamentoAdicionado = null;
        _pagamentoRepo.Setup(r => r.AdicionarAsync(It.IsAny<PagamentoTreinador>(), It.IsAny<CancellationToken>()))
            .Callback<PagamentoTreinador, CancellationToken>((p, _) => pagamentoAdicionado = p);

        var result = await _handler.HandleAsync(new GerarCobrancaPlanoTreinadorCommand(assinatura.Id));

        result.IsSuccess.Should().BeTrue();
        pagamentoAdicionado.Should().NotBeNull();
        pagamentoAdicionado!.StripePaymentIntentId.Should().NotBeNullOrEmpty("pagamento só persiste após Stripe retornar intent id (G-PAY-1)");
        pagamentoAdicionado.Status.Should().Be(PagamentoStatus.Pendente);
        pagamentoAdicionado.Finalidade.Should().Be(FinalidadePagamentoTreinador.Renovacao);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Idempotencia_IntentIdDerivadoDoPagamentoId()
    {
        var assinatura = CriarAssinaturaAtiva();
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        var fakeStripe = new FakeStripeService();
        var criarPagamentoService = new CriarPagamentoComIntentService(
            _unitOfWork.Object, _transactionProvider.Object, TimeProvider.System,
            Mock.Of<ILogger<CriarPagamentoComIntentService>>());
        var handler = new GerarCobrancaPlanoTreinadorHandler(
            _assinaturaRepo.Object, _pagamentoRepo.Object, _planoRepo.Object,
            fakeStripe, _unitOfWork.Object, criarPagamentoService,
            TimeProvider.System, _logger.Object);

        PagamentoTreinador? pagamentoAdicionado = null;
        _pagamentoRepo.Setup(r => r.AdicionarAsync(It.IsAny<PagamentoTreinador>(), It.IsAny<CancellationToken>()))
            .Callback<PagamentoTreinador, CancellationToken>((p, _) => pagamentoAdicionado = p);

        var result = await handler.HandleAsync(new GerarCobrancaPlanoTreinadorCommand(assinatura.Id));

        result.IsSuccess.Should().BeTrue();
        pagamentoAdicionado.Should().NotBeNull();
        pagamentoAdicionado!.StripePaymentIntentId.Should().Be($"pi_fake_treinador_{pagamentoAdicionado.Id:N}",
            "o intent id deve ser derivado do PagamentoId para garantir idempotência em retries");
    }

    [Fact]
    public async Task HandleAsync_PlanoAgendadoExistente_AplicaNovoValorAntesDeGerar()
    {
        var planoNovo = PlanoPlataforma.Criar("Pro", TierPlano.Pro, 50, 100m, DateTime.UtcNow).Value;
        var assinatura = CriarAssinaturaAtiva();
        assinatura.AgendarDowngrade(planoNovo.Id, DateTime.UtcNow);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoNovo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoNovo);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        PagamentoTreinador? pagamentoAdicionado = null;
        _pagamentoRepo.Setup(r => r.AdicionarAsync(It.IsAny<PagamentoTreinador>(), It.IsAny<CancellationToken>()))
            .Callback<PagamentoTreinador, CancellationToken>((p, _) => pagamentoAdicionado = p);

        var result = await _handler.HandleAsync(new GerarCobrancaPlanoTreinadorCommand(assinatura.Id));

        result.IsSuccess.Should().BeTrue();
        pagamentoAdicionado!.Valor.Should().Be(100m, "cobrança usa o valor do plano recém-aplicado (R$100), não o antigo (R$50)");
        assinatura.PlanoPlataformaIdAgendado.Should().BeNull("AplicarPlanoAgendado deve limpar o campo após aplicar");
        assinatura.Valor.Should().Be(100m, "assinatura assume o novo valor");
    }

    [Fact]
    public async Task HandleAsync_FreePlanoAgendado_CancelaAssinaturaRetornaCodigoEspecifico()
    {
        var planoFree = PlanoPlataforma.Criar("Free", TierPlano.Free, 5, 0m, DateTime.UtcNow).Value;
        var assinatura = CriarAssinaturaAtiva();
        assinatura.AgendarDowngrade(planoFree.Id, DateTime.UtcNow);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoFree.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoFree);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        var result = await _handler.HandleAsync(new GerarCobrancaPlanoTreinadorCommand(assinatura.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("plano_free_assinatura_cancelada");
        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Cancelada, "assinatura cancelada no downgrade para Free");
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never, "sem cobrança no Free");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once, "cancela e comita");
    }

    [Fact]
    public async Task HandleAsync_PlanoAgendadoNaoEncontrado_LimpaAgendamentoECobraAtual()
    {
        var planoExcluido = Guid.NewGuid();
        var assinatura = CriarAssinaturaAtiva();
        assinatura.AgendarDowngrade(planoExcluido, DateTime.UtcNow);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoExcluido, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlanoPlataforma?)null);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        PagamentoTreinador? pagamentoAdicionado = null;
        _pagamentoRepo.Setup(r => r.AdicionarAsync(It.IsAny<PagamentoTreinador>(), It.IsAny<CancellationToken>()))
            .Callback<PagamentoTreinador, CancellationToken>((p, _) => pagamentoAdicionado = p);

        var result = await _handler.HandleAsync(new GerarCobrancaPlanoTreinadorCommand(assinatura.Id));

        result.IsSuccess.Should().BeTrue("plano deletado não impede cobrança — usa valor atual");
        pagamentoAdicionado!.Valor.Should().Be(50m, "cobra no valor original pois agendamento foi descartado");
        assinatura.PlanoPlataformaIdAgendado.Should().BeNull("LimparPlanoAgendado deve ser chamado ao detectar plano excluído");
    }
}
