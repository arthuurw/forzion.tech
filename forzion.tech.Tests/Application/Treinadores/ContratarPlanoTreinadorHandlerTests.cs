using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Application.UseCases.Treinadores.ContratarPlanoTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using forzion.tech.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class ContratarPlanoTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IAssinaturaTreinadorRepository> _assinaturaRepo = new();
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<IPagamentoTreinadorRepository> _pagamentoRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDbContextTransactionProvider> _transactionProvider = new();
    private readonly Mock<IDatabaseErrorInspector> _errorInspector = new();
    private readonly ContratarPlanoTreinadorHandler _handler;

    private static readonly PixPaymentResult PixResult = new("pi_contratacao_123", "qrcode", "https://img", DateTime.UtcNow.AddHours(1));

    public ContratarPlanoTreinadorHandlerTests()
    {
        _transactionProvider.SetupExecuteInTransaction<Result<(PagamentoTreinador, string?)>>();

        _stripeService.Setup(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PixResult);

        var criarPagamentoService = new CriarPagamentoComIntentService(
            _unitOfWork.Object, _transactionProvider.Object, _errorInspector.Object, _stripeService.Object, TimeProvider.System,
            Mock.Of<ILogger<CriarPagamentoComIntentService>>());

        _handler = new ContratarPlanoTreinadorHandler(
            _treinadorRepo.Object, _assinaturaRepo.Object, _planoRepo.Object,
            _pagamentoRepo.Object, _stripeService.Object, criarPagamentoService,
            _unitOfWork.Object, _errorInspector.Object, TimeProvider.System,
            Mock.Of<ILogger<ContratarPlanoTreinadorHandler>>());
    }

    private static Treinador TreinadorAtivo()
    {
        var t = Treinador.Criar(Guid.NewGuid(), "Trainer Ativo", DateTime.UtcNow).Value;
        t.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        return t;
    }

    private static PlanoPlataforma PlanoValido(decimal preco = 100m, TierPlano tier = TierPlano.Pro)
        => PlanoPlataforma.Criar($"Plano {tier}", tier, 50, preco, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_TreinadorNaoAtivo_RetornaConflictoNaoElegivel()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Trainer Pendente", DateTime.UtcNow).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new ContratarPlanoTreinadorCommand(treinador.Id, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("treinador.nao_elegivel_contratacao");
    }

    [Fact]
    public async Task HandleAsync_PlanoNaoEncontrado_RetornaNotFound()
    {
        var treinador = TreinadorAtivo();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlanoPlataforma?)null);

        var result = await _handler.HandleAsync(new ContratarPlanoTreinadorCommand(treinador.Id, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("plano_plataforma_nao_encontrado");
    }

    [Fact]
    public async Task HandleAsync_PlanoInativo_RetornaFailure()
    {
        var treinador = TreinadorAtivo();
        var plano = PlanoPlataforma.Criar("Plano Inativo", TierPlano.Basic, 50, 50m, DateTime.UtcNow).Value;
        plano.Inativar(DateTime.UtcNow);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);

        var result = await _handler.HandleAsync(new ContratarPlanoTreinadorCommand(treinador.Id, plano.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("plano_plataforma.inativo");
    }

    [Fact]
    public async Task HandleAsync_PlanoElite_RetornaEliteIndisponivel()
    {
        var treinador = TreinadorAtivo();
        var plano = PlanoValido(500m, TierPlano.Elite);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);

        var result = await _handler.HandleAsync(new ContratarPlanoTreinadorCommand(treinador.Id, plano.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("plano_plataforma.elite_indisponivel");
    }

    [Fact]
    public async Task HandleAsync_AssinaturaAtivaExistente_RetornaConflictoJaExiste()
    {
        var treinador = TreinadorAtivo();
        var plano = PlanoValido();
        var assinatura = AssinaturaTreinador.Criar(treinador.Id, Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new ContratarPlanoTreinadorCommand(treinador.Id, plano.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("treinador.assinatura_ja_existe");
    }

    [Fact]
    public async Task HandleAsync_TreinadorAtivoSemAssinatura_CriaAssinaturaPendenteComValorDoPlanoCriaPagamentoContratacao()
    {
        var treinador = TreinadorAtivo();
        var plano = PlanoValido(150m);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaTreinador?)null);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        PagamentoTreinador? pagamentoAdicionado = null;
        _pagamentoRepo.Setup(r => r.AdicionarAsync(It.IsAny<PagamentoTreinador>(), It.IsAny<CancellationToken>()))
            .Callback<PagamentoTreinador, CancellationToken>((p, _) => pagamentoAdicionado = p);

        var result = await _handler.HandleAsync(new ContratarPlanoTreinadorCommand(treinador.Id, plano.Id));

        result.IsSuccess.Should().BeTrue();
        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<AssinaturaTreinador>(), It.IsAny<CancellationToken>()), Times.Once);
        pagamentoAdicionado.Should().NotBeNull();
        pagamentoAdicionado!.Finalidade.Should().Be(FinalidadePagamentoTreinador.Contratacao);
        pagamentoAdicionado.Valor.Should().Be(plano.Preco);
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            plano.Preco, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaPendenteMesmoPlano_ReusaSemCriarNovaNemChamarStripe()
    {
        var treinador = TreinadorAtivo();
        var plano = PlanoValido(100m);
        var assinaturaPendente = AssinaturaTreinador.Criar(treinador.Id, plano.Id, 100m, DateTime.UtcNow).Value;
        var pagamentoPendente = PagamentoTreinador.Criar(
            treinador.Id, assinaturaPendente.Id, 100m, FinalidadePagamentoTreinador.Contratacao, DateTime.UtcNow).Value;
        pagamentoPendente.DefinirDadosPix("pi_existing_123", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinaturaPendente);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinaturaPendente.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamentoPendente);

        var result = await _handler.HandleAsync(new ContratarPlanoTreinadorCommand(treinador.Id, plano.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.PagamentoId.Should().Be(pagamentoPendente.Id, "idempotência: reusa o intent pendente existente");
        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<AssinaturaTreinador>(), It.IsAny<CancellationToken>()), Times.Never);
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaExcecao()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new ContratarPlanoTreinadorCommand(Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
