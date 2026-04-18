using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Services;

public class LimiteTreinadorServiceTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPlanoTreinadorRepository> _planoRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly LimiteTreinadorService _service;

    public LimiteTreinadorServiceTests()
    {
        _service = new LimiteTreinadorService(_treinadorRepo.Object, _planoRepo.Object, _vinculoRepo.Object);
    }

    [Fact]
    public async Task ValidarAsync_AbaixoDoLimite_NaoLanca()
    {
        var planoId = Guid.NewGuid();
        var treinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Ana");
        treinador.AtribuirPlano(planoId);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlanoTreinador.Criar("Starter", 10, 99m));
        _vinculoRepo.Setup(r => r.ContarAtivosPorTreinadorAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var act = async () => await _service.ValidarAsync(treinadorId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidarAsync_LimiteAtingido_LancaException()
    {
        var planoId = Guid.NewGuid();
        var treinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Ana");
        treinador.AtribuirPlano(planoId);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlanoTreinador.Criar("Starter", 5, 99m));
        _vinculoRepo.Setup(r => r.ContarAtivosPorTreinadorAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var act = async () => await _service.ValidarAsync(treinadorId);
        await act.Should().ThrowAsync<LimiteAlunosAtingidoException>();
    }

    [Fact]
    public async Task ValidarAsync_SemPlano_NaoLanca()
    {
        var treinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Ana");

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var act = async () => await _service.ValidarAsync(treinadorId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidarAsync_TreinadorNaoEncontrado_LancaDomainException()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var act = async () => await _service.ValidarAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>();
    }
}
