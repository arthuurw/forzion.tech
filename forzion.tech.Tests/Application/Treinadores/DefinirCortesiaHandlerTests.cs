using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Application.UseCases.Treinadores.DefinirCortesia;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class DefinirCortesiaHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<IAssinaturaTreinadorRepository> _assinaturaRepo = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<DefinirCortesiaHandler>> _logger = new();
    private readonly DefinirCortesiaHandler _handler;

    public DefinirCortesiaHandlerTests()
    {
        _handler = new DefinirCortesiaHandler(
            _treinadorRepo.Object, _planoRepo.Object, _assinaturaRepo.Object, _logRepo.Object,
            _unitOfWork.Object, TimeProvider.System, _logger.Object);
    }

    private static AssinaturaTreinador CriarAssinaturaAtiva(Guid planoId, decimal valor)
    {
        var assinatura = AssinaturaTreinador.Criar(Guid.NewGuid(), planoId, valor, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);
        return assinatura;
    }

    [Fact]
    public async Task HandleAsync_CortesiaAbaixoDoValorPago_RetornaFailureECommitNuncaChamado()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var planoAssinado = PlanoPlataforma.Criar("Pro", TierPlano.Pro, 20, 99m, DateTime.UtcNow).Value;
        var planoCortesia = PlanoPlataforma.Criar("Starter", TierPlano.Basic, 5, 49m, DateTime.UtcNow).Value;
        var assinatura = CriarAssinaturaAtiva(planoAssinado.Id, 99m);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoCortesia.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoCortesia);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoAssinado.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoAssinado);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new DefinirCortesiaCommand(treinador.Id, planoCortesia.Id, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("treinador.cortesia_abaixo_do_pago");
        treinador.PlanoCortesiaId.Should().BeNull();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CortesiaIgualAoValorPago_Aceita()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var planoAssinado = PlanoPlataforma.Criar("Pro", TierPlano.Pro, 20, 99m, DateTime.UtcNow).Value;
        var planoCortesia = PlanoPlataforma.Criar("ProCortesia", TierPlano.Pro, 20, 99m, DateTime.UtcNow).Value;
        var assinatura = CriarAssinaturaAtiva(planoAssinado.Id, 99m);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoCortesia.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoCortesia);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoAssinado.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoAssinado);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new DefinirCortesiaCommand(treinador.Id, planoCortesia.Id, Guid.NewGuid()));

        result.IsSuccess.Should().BeTrue();
        treinador.PlanoCortesiaId.Should().Be(planoCortesia.Id);
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CortesiaAcimaDoValorPago_Aceita()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var planoAssinado = PlanoPlataforma.Criar("Pro", TierPlano.Pro, 20, 99m, DateTime.UtcNow).Value;
        var planoCortesia = PlanoPlataforma.Criar("ProPlus", TierPlano.ProPlus, 50, 199m, DateTime.UtcNow).Value;
        var assinatura = CriarAssinaturaAtiva(planoAssinado.Id, 99m);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoCortesia.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoCortesia);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoAssinado.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoAssinado);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new DefinirCortesiaCommand(treinador.Id, planoCortesia.Id, Guid.NewGuid()));

        result.IsSuccess.Should().BeTrue();
        treinador.PlanoCortesiaId.Should().Be(planoCortesia.Id);
    }

    [Fact]
    public async Task HandleAsync_PrecoDoCatalogoSubiuAposAssinatura_UsaPrecoAtualDoPlanoNaoSnapshot()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var planoAssinado = PlanoPlataforma.Criar("Pro", TierPlano.Pro, 20, 50m, DateTime.UtcNow).Value;
        var assinatura = CriarAssinaturaAtiva(planoAssinado.Id, 50m);
        planoAssinado.Atualizar(nome: null, tier: null, maxAlunos: null, preco: 150m, agora: DateTime.UtcNow);
        var planoCortesia = PlanoPlataforma.Criar("Starter", TierPlano.Basic, 5, 100m, DateTime.UtcNow).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoCortesia.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoCortesia);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoAssinado.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoAssinado);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new DefinirCortesiaCommand(treinador.Id, planoCortesia.Id, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("treinador.cortesia_abaixo_do_pago");
        treinador.PlanoCortesiaId.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_SemAssinaturaAtiva_ValorPagoZero_AceitaQualquerPrecoDeCortesia()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var planoCortesia = PlanoPlataforma.Criar("Starter", TierPlano.Basic, 5, 1m, DateTime.UtcNow).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoCortesia.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoCortesia);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync((AssinaturaTreinador?)null);

        var result = await _handler.HandleAsync(new DefinirCortesiaCommand(treinador.Id, planoCortesia.Id, Guid.NewGuid()));

        result.IsSuccess.Should().BeTrue();
        treinador.PlanoCortesiaId.Should().Be(planoCortesia.Id);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaNaoAtiva_ValorPagoZero_AceitaQualquerPrecoDeCortesia()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var planoAssinado = PlanoPlataforma.Criar("Pro", TierPlano.Pro, 20, 99m, DateTime.UtcNow).Value;
        var planoCortesia = PlanoPlataforma.Criar("Starter", TierPlano.Basic, 5, 1m, DateTime.UtcNow).Value;
        var assinaturaCancelada = AssinaturaTreinador.Criar(Guid.NewGuid(), planoAssinado.Id, 99m, DateTime.UtcNow).Value;
        assinaturaCancelada.Cancelar(DateTime.UtcNow);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoCortesia.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoCortesia);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinaturaCancelada);

        var result = await _handler.HandleAsync(new DefinirCortesiaCommand(treinador.Id, planoCortesia.Id, Guid.NewGuid()));

        result.IsSuccess.Should().BeTrue();
        treinador.PlanoCortesiaId.Should().Be(planoCortesia.Id);
    }

    [Fact]
    public async Task HandleAsync_RemoverCortesiaComPlanoIdNulo_AceitaSempreIndependenteDoValorPago()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var planoCortesiaAnterior = PlanoPlataforma.Criar("ProPlus", TierPlano.ProPlus, 50, 199m, DateTime.UtcNow).Value;
        treinador.DefinirCortesia(planoCortesiaAnterior.Id, DateTime.UtcNow);
        var planoAssinado = PlanoPlataforma.Criar("Pro", TierPlano.Pro, 20, 99m, DateTime.UtcNow).Value;
        var assinatura = CriarAssinaturaAtiva(planoAssinado.Id, 99m);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new DefinirCortesiaCommand(treinador.Id, null, Guid.NewGuid()));

        result.IsSuccess.Should().BeTrue();
        treinador.PlanoCortesiaId.Should().BeNull();
        _planoRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PlanoElite_RetornaFailureEliteIndisponivel_ECommitNuncaChamado()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var planoElite = PlanoPlataforma.Criar("Elite", TierPlano.Elite, 100, 999m, DateTime.UtcNow).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoElite.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoElite);

        var result = await _handler.HandleAsync(new DefinirCortesiaCommand(treinador.Id, planoElite.Id, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("plano_plataforma.elite_indisponivel");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PlanoNaoEncontrado_LancaException()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((PlanoPlataforma?)null);

        var act = async () => await _handler.HandleAsync(new DefinirCortesiaCommand(treinador.Id, Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>().WithMessage("Plano não encontrado.");
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaException()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new DefinirCortesiaCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorInativo_RetornaFailureECommitNuncaChamado()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.Inativar(DateTime.UtcNow);
        var planoCortesia = PlanoPlataforma.Criar("Starter", TierPlano.Basic, 5, 49m, DateTime.UtcNow).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoCortesia.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoCortesia);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync((AssinaturaTreinador?)null);

        var result = await _handler.HandleAsync(new DefinirCortesiaCommand(treinador.Id, planoCortesia.Id, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("treinador.plano_treinador_inativo");
        treinador.PlanoCortesiaId.Should().BeNull();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CortesiaDefinidaComSucesso_PlanoEfetivoResolverPassaARefletirImediatamente()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var planoCortesia = PlanoPlataforma.Criar("ProPlus", TierPlano.ProPlus, 50, 199m, DateTime.UtcNow).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoCortesia.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoCortesia);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync((AssinaturaTreinador?)null);

        var result = await _handler.HandleAsync(new DefinirCortesiaCommand(treinador.Id, planoCortesia.Id, Guid.NewGuid()));
        result.IsSuccess.Should().BeTrue();

        var resolver = new PlanoEfetivoResolver(
            _treinadorRepo.Object, _assinaturaRepo.Object, _planoRepo.Object, Mock.Of<ILogger<PlanoEfetivoResolver>>());

        var planoEfetivo = await resolver.ResolverAsync(treinador.Id);

        planoEfetivo.PlanoId.Should().Be(planoCortesia.Id);
        planoEfetivo.Tier.Should().Be(TierPlano.ProPlus);
    }
}
