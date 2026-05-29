using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ExcluirGrupoMuscular;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared;
using Moq;

namespace forzion.tech.Tests.Application.Admin.GruposMusculares;

public class ExcluirGrupoMuscularHandlerTests
{
    private readonly Mock<IGrupoMuscularRepository> _repository = new();
    private readonly Mock<IExercicioRepository> _exercicioRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ExcluirGrupoMuscularHandler _handler;

    public ExcluirGrupoMuscularHandlerTests()
    {
        _handler = new ExcluirGrupoMuscularHandler(_repository.Object, _exercicioRepository.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task HandleAsync_GrupoExiste_ExcluiEComita()
    {
        var grupo = GrupoMuscular.Criar("Peito", DateTime.UtcNow).Value;
        _repository.Setup(r => r.ObterPorIdAsync(grupo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grupo);
        _exercicioRepository.Setup(r => r.ExisteComGrupoMuscularAsync(grupo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _handler.HandleAsync(new ExcluirGrupoMuscularCommand(grupo.Id));

        result.IsSuccess.Should().BeTrue();
        _repository.Verify(r => r.Excluir(grupo), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_GrupoEmUso_RetornaFailureENaoExclui()
    {
        var grupo = GrupoMuscular.Criar("Peito", DateTime.UtcNow).Value;
        _repository.Setup(r => r.ObterPorIdAsync(grupo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grupo);
        _exercicioRepository.Setup(r => r.ExisteComGrupoMuscularAsync(grupo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _handler.HandleAsync(new ExcluirGrupoMuscularCommand(grupo.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("grupo_muscular_em_uso");
        _repository.Verify(r => r.Excluir(It.IsAny<GrupoMuscular>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_GrupoNaoEncontrado_RetornaFailureNotFound()
    {
        _repository.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((GrupoMuscular?)null);

        var result = await _handler.HandleAsync(new ExcluirGrupoMuscularCommand(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("grupo_muscular_nao_encontrado");
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_GrupoNaoEncontrado_NaoExclui()
    {
        _repository.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((GrupoMuscular?)null);

        await _handler.HandleAsync(new ExcluirGrupoMuscularCommand(Guid.NewGuid()));

        _repository.Verify(r => r.Excluir(It.IsAny<GrupoMuscular>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
