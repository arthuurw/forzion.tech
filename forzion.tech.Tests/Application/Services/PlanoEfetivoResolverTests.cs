using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Services;

public class PlanoEfetivoResolverTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IAssinaturaTreinadorRepository> _assinaturaRepo = new();
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<ILogger<PlanoEfetivoResolver>> _logger = new();
    private readonly PlanoEfetivoResolver _resolver;

    private static readonly Guid TreinadorId = Guid.NewGuid();

    public PlanoEfetivoResolverTests()
    {
        _resolver = new PlanoEfetivoResolver(_treinadorRepo.Object, _assinaturaRepo.Object, _planoRepo.Object, _logger.Object);
    }

    private static PlanoPlataforma CriarPlano(string nome, TierPlano tier, int maxAlunos, decimal preco) =>
        PlanoPlataforma.Criar(nome, tier, maxAlunos, preco, DateTime.UtcNow).Value;

    private static Treinador CriarTreinador(Guid? planoCortesiaId = null)
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        if (planoCortesiaId is not null)
            treinador.DefinirCortesia(planoCortesiaId, DateTime.UtcNow);
        return treinador;
    }

    private static AssinaturaTreinador CriarAssinatura(Guid planoId, decimal valor, AssinaturaTreinadorStatus status)
    {
        var assinatura = AssinaturaTreinador.Criar(TreinadorId, planoId, valor, DateTime.UtcNow).Value;
        switch (status)
        {
            case AssinaturaTreinadorStatus.Ativa:
                assinatura.Ativar(DateTime.UtcNow);
                break;
            case AssinaturaTreinadorStatus.Inadimplente:
                assinatura.Ativar(DateTime.UtcNow);
                assinatura.MarcarInadimplente(DateTime.UtcNow);
                break;
            case AssinaturaTreinadorStatus.Cancelada:
                assinatura.Cancelar(DateTime.UtcNow);
                break;
            case AssinaturaTreinadorStatus.Pendente:
            default:
                break;
        }
        return assinatura;
    }

    [Fact]
    public async Task ResolverAsync_SemAssinaturaSemCortesia_RetornaPlanoFree()
    {
        var free = CriarPlano("Free", TierPlano.Free, 3, 0m);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(CriarTreinador());
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(TreinadorId, It.IsAny<CancellationToken>())).ReturnsAsync((AssinaturaTreinador?)null);
        _planoRepo.Setup(r => r.ObterPlanoFreeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(free);

        var resultado = await _resolver.ResolverAsync(TreinadorId);

        resultado.Should().Be(new PlanoEfetivo(free.Id, TierPlano.Free, 3, true));
    }

    [Fact]
    public async Task ResolverAsync_AssinaturaAtiva_SemCortesia_RetornaPlanoDaAssinatura()
    {
        var proPlano = CriarPlano("Pro", TierPlano.Pro, 20, 99m);
        var assinatura = CriarAssinatura(proPlano.Id, proPlano.Preco, AssinaturaTreinadorStatus.Ativa);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(CriarTreinador());
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(TreinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _planoRepo.Setup(r => r.ObterPorIdAsync(proPlano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proPlano);

        var resultado = await _resolver.ResolverAsync(TreinadorId);

        resultado.Should().Be(new PlanoEfetivo(proPlano.Id, TierPlano.Pro, 20, false));
    }

    [Theory]
    [InlineData(AssinaturaTreinadorStatus.Pendente)]
    [InlineData(AssinaturaTreinadorStatus.Inadimplente)]
    [InlineData(AssinaturaTreinadorStatus.Cancelada)]
    public async Task ResolverAsync_AssinaturaNaoAtivaSemCortesia_RetornaPlanoFree(AssinaturaTreinadorStatus status)
    {
        var proPlano = CriarPlano("Pro", TierPlano.Pro, 20, 99m);
        var free = CriarPlano("Free", TierPlano.Free, 3, 0m);
        var assinatura = CriarAssinatura(proPlano.Id, proPlano.Preco, status);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(CriarTreinador());
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(TreinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _planoRepo.Setup(r => r.ObterPlanoFreeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(free);

        var resultado = await _resolver.ResolverAsync(TreinadorId);

        resultado.Should().Be(new PlanoEfetivo(free.Id, TierPlano.Free, 3, true));
        _planoRepo.Verify(r => r.ObterPorIdAsync(proPlano.Id, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolverAsync_ProAtivaComCortesiaProPlus_RetornaProPlusPorSerMaisCaro()
    {
        var proPlano = CriarPlano("Pro", TierPlano.Pro, 20, 99m);
        var proPlusPlano = CriarPlano("ProPlus", TierPlano.ProPlus, 50, 199m);
        var assinatura = CriarAssinatura(proPlano.Id, proPlano.Preco, AssinaturaTreinadorStatus.Ativa);
        var treinador = CriarTreinador(proPlusPlano.Id);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(TreinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _planoRepo.Setup(r => r.ObterPorIdAsync(proPlano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proPlano);
        _planoRepo.Setup(r => r.ObterPorIdAsync(proPlusPlano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proPlusPlano);

        var resultado = await _resolver.ResolverAsync(TreinadorId);

        resultado.Should().Be(new PlanoEfetivo(proPlusPlano.Id, TierPlano.ProPlus, 50, false));
    }

    [Fact]
    public async Task ResolverAsync_AssinaturaLapsaParaInadimplenteComCortesiaProPlus_CortesiaSeguraOChao()
    {
        var proPlano = CriarPlano("Pro", TierPlano.Pro, 20, 99m);
        var proPlusPlano = CriarPlano("ProPlus", TierPlano.ProPlus, 50, 199m);
        var assinatura = CriarAssinatura(proPlano.Id, proPlano.Preco, AssinaturaTreinadorStatus.Inadimplente);
        var treinador = CriarTreinador(proPlusPlano.Id);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(TreinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _planoRepo.Setup(r => r.ObterPorIdAsync(proPlusPlano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proPlusPlano);

        var resultado = await _resolver.ResolverAsync(TreinadorId);

        resultado.Should().Be(new PlanoEfetivo(proPlusPlano.Id, TierPlano.ProPlus, 50, false));
        _planoRepo.Verify(r => r.ObterPorIdAsync(proPlano.Id, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolverAsync_SemFreeConfigurado_FailClosaComCapZeroELogaErro()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(CriarTreinador());
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(TreinadorId, It.IsAny<CancellationToken>())).ReturnsAsync((AssinaturaTreinador?)null);
        _planoRepo.Setup(r => r.ObterPlanoFreeAsync(It.IsAny<CancellationToken>())).ReturnsAsync((PlanoPlataforma?)null);

        var resultado = await _resolver.ResolverAsync(TreinadorId);

        resultado.Should().Be(new PlanoEfetivo(null, TierPlano.Free, 0, true));
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
