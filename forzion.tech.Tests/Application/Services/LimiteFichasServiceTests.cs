using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Services;

public class LimiteFichasServiceTests
{
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IPacoteAlunoRepository> _pacoteRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly LimiteFichasService _service;

    public LimiteFichasServiceTests()
    {
        _service = new LimiteFichasService(_vinculoRepo.Object, _pacoteRepo.Object, _treinoAlunoRepo.Object);
    }

    [Fact]
    public async Task ValidarAsync_AbaixoDoLimite_NaoLanca()
    {
        var alunoId = Guid.NewGuid();
        var pacoteId = Guid.NewGuid();
        var vinculo = CriarVinculoComPacote(pacoteId);

        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PacoteAluno.Criar(Guid.NewGuid(), "Básico", 3, 49m));
        _treinoAlunoRepo.Setup(r => r.ContarAtivosPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var act = async () => await _service.ValidarAsync(alunoId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidarAsync_LimiteAtingido_LancaException()
    {
        var alunoId = Guid.NewGuid();
        var pacoteId = Guid.NewGuid();
        var vinculo = CriarVinculoComPacote(pacoteId);

        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PacoteAluno.Criar(Guid.NewGuid(), "Básico", 2, 49m));
        _treinoAlunoRepo.Setup(r => r.ContarAtivosPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var act = async () => await _service.ValidarAsync(alunoId);
        await act.Should().ThrowAsync<LimiteFichasAtingidoException>();
    }

    [Fact]
    public async Task ValidarAsync_SemVinculo_NaoLanca()
    {
        var alunoId = Guid.NewGuid();
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);

        var act = async () => await _service.ValidarAsync(alunoId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidarAsync_SemPacote_NaoLanca()
    {
        var alunoId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(Guid.NewGuid(), alunoId);

        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);

        var act = async () => await _service.ValidarAsync(alunoId);
        await act.Should().NotThrowAsync();
    }

    private static VinculoTreinadorAluno CriarVinculoComPacote(Guid pacoteId)
    {
        var vinculo = VinculoTreinadorAluno.Criar(Guid.NewGuid(), Guid.NewGuid());
        vinculo.Aprovar(Guid.NewGuid(), pacoteId);
        return vinculo;
    }
}
