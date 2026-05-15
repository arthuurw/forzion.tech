using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ExcluirGrupoMuscular;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Admin.GruposMusculares;

public class ExcluirGrupoMuscularHandlerTests
{
    private readonly Mock<IGrupoMuscularRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ExcluirGrupoMuscularHandler _handler;

    public ExcluirGrupoMuscularHandlerTests()
    {
        _handler = new ExcluirGrupoMuscularHandler(_repository.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task HandleAsync_GrupoExiste_ExcluiEComita()
    {
        var grupo = GrupoMuscular.Criar("Peito");
        _repository.Setup(r => r.ObterPorIdAsync(grupo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grupo);

        await _handler.HandleAsync(new ExcluirGrupoMuscularCommand(grupo.Id));

        _repository.Verify(r => r.Excluir(grupo), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_GrupoNaoEncontrado_LancaDomainException()
    {
        _repository.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((GrupoMuscular?)null);

        var act = async () => await _handler.HandleAsync(new ExcluirGrupoMuscularCommand(Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*não encontrado*");
    }

    [Fact]
    public async Task HandleAsync_GrupoNaoEncontrado_NaoExclui()
    {
        _repository.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((GrupoMuscular?)null);

        try { await _handler.HandleAsync(new ExcluirGrupoMuscularCommand(Guid.NewGuid())); } catch { }

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
