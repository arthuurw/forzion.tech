using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Services;

public class LimiteTreinadorServiceTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPlanoEfetivoResolver> _planoEfetivoResolver = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly LimiteTreinadorService _service;

    public LimiteTreinadorServiceTests()
    {
        _service = new LimiteTreinadorService(_treinadorRepo.Object, _planoEfetivoResolver.Object, _vinculoRepo.Object);
    }

    [Fact]
    public async Task ValidarAsync_AbaixoDoLimite_NaoLanca()
    {
        var treinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Ana", DateTime.UtcNow).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoEfetivoResolver.Setup(r => r.ResolverAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanoEfetivo(Guid.NewGuid(), TierPlano.Basic, 10, false));
        _vinculoRepo.Setup(r => r.ContarAtivosPorTreinadorAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var act = async () => await _service.ValidarAsync(treinadorId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidarAsync_LimiteAtingido_LancaException()
    {
        var treinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Ana", DateTime.UtcNow).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoEfetivoResolver.Setup(r => r.ResolverAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanoEfetivo(Guid.NewGuid(), TierPlano.Basic, 5, false));
        _vinculoRepo.Setup(r => r.ContarAtivosPorTreinadorAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var act = async () => await _service.ValidarAsync(treinadorId);
        await act.Should().ThrowAsync<LimiteAlunosAtingidoException>();
    }

    [Fact]
    public async Task ValidarAsync_TreinadorNaoEncontrado_LancaException()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var act = async () => await _service.ValidarAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task ValidarAsync_AssinaturaPendenteCapEfetivoFree_BloqueiaAcimaDoCapFree()
    {
        var treinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Ana", DateTime.UtcNow).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoEfetivoResolver.Setup(r => r.ResolverAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanoEfetivo(Guid.NewGuid(), TierPlano.Free, 3, true));
        _vinculoRepo.Setup(r => r.ContarAtivosPorTreinadorAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var act = async () => await _service.ValidarAsync(treinadorId);
        await act.Should().ThrowAsync<LimiteAlunosAtingidoException>();
    }

    [Fact]
    public async Task ValidarAsync_AssinaturaAtivaPlanoPro_PermiteAteCapPro()
    {
        var treinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Ana", DateTime.UtcNow).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoEfetivoResolver.Setup(r => r.ResolverAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanoEfetivo(Guid.NewGuid(), TierPlano.Pro, 20, false));
        _vinculoRepo.Setup(r => r.ContarAtivosPorTreinadorAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(19);

        var act = async () => await _service.ValidarAsync(treinadorId);
        await act.Should().NotThrowAsync();
    }
}
