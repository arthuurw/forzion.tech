using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.AtualizarGrupoMuscular;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Admin.GruposMusculares;

public class AtualizarGrupoMuscularHandlerTests
{
    private readonly Mock<IGrupoMuscularRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly AtualizarGrupoMuscularHandler _handler;

    public AtualizarGrupoMuscularHandlerTests()
    {
        _handler = new AtualizarGrupoMuscularHandler(_repository.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_AtualizaERetornaResponse()
    {
        var grupo = GrupoMuscular.Criar("Peito");
        _repository.Setup(r => r.ObterPorIdAsync(grupo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grupo);
        _repository.Setup(r => r.ObterPorNomeAsync("Costas", It.IsAny<CancellationToken>())).ReturnsAsync((GrupoMuscular?)null);

        var result = await _handler.HandleAsync(new AtualizarGrupoMuscularCommand(grupo.Id, "Costas"));

        result.Nome.Should().Be("Costas");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_GrupoNaoEncontrado_LancaDomainException()
    {
        _repository.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((GrupoMuscular?)null);

        var act = async () => await _handler.HandleAsync(new AtualizarGrupoMuscularCommand(Guid.NewGuid(), "Costas"));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*não encontrado*");
    }

    [Fact]
    public async Task HandleAsync_NomeDuplicadoOutroGrupo_LancaDomainException()
    {
        var grupo = GrupoMuscular.Criar("Peito");
        var outro = GrupoMuscular.Criar("Costas");
        _repository.Setup(r => r.ObterPorIdAsync(grupo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grupo);
        _repository.Setup(r => r.ObterPorNomeAsync("Costas", It.IsAny<CancellationToken>())).ReturnsAsync(outro);

        var act = async () => await _handler.HandleAsync(new AtualizarGrupoMuscularCommand(grupo.Id, "Costas"));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Já existe outro*");
    }

    [Fact]
    public async Task HandleAsync_MesmoNomeMesmoId_NaoLancaException()
    {
        var grupo = GrupoMuscular.Criar("Peito");
        _repository.Setup(r => r.ObterPorIdAsync(grupo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grupo);
        _repository.Setup(r => r.ObterPorNomeAsync("Peito", It.IsAny<CancellationToken>())).ReturnsAsync(grupo);

        var act = async () => await _handler.HandleAsync(new AtualizarGrupoMuscularCommand(grupo.Id, "Peito"));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
