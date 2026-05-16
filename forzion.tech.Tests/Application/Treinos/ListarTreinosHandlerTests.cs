using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.ListarTreinos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class ListarTreinosHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<ListarTreinosHandler>> _logger = new();
    private readonly ListarTreinosHandler _handler;

    public ListarTreinosHandlerTests()
    {
        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.IsAluno).Returns(false);
        _userContext.Setup(c => c.IsTreinador).Returns(true);
        _handler = new ListarTreinosHandler(
            _treinoRepo.Object, _vinculoRepo.Object, _userContext.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_Treinador_ComVinculo_RetornaListaPaginada()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var treinos = new List<Treino>
        {
            Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId),
            Treino.Criar("Treino B", ObjetivoTreino.Forca, treinadorId)
        };

        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(VinculoTreinadorAluno.Criar(treinadorId, alunoId));
        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Treino>)treinos, 2));

        var result = await _handler.HandleAsync(new ListarTreinosQuery(alunoId, 1, 10));

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Pagina.Should().Be(1);
        result.TamanhoPagina.Should().Be(10);
    }

    [Fact]
    public async Task HandleAsync_Treinador_SemVinculo_LancaAcessoNegadoException()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();

        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        var act = async () => await _handler.HandleAsync(new ListarTreinosQuery(alunoId, 1, 10));

        await act.Should().ThrowAsync<AcessoNegadoException>();
        _treinoRepo.Verify(r => r.ListarPorAlunoAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SystemAdmin_AcessaQualquerAluno()
    {
        var alunoId = Guid.NewGuid();
        _userContext.Setup(c => c.IsSystemAdmin).Returns(true);
        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Treino>)[], 0));

        var act = async () => await _handler.HandleAsync(new ListarTreinosQuery(alunoId, 1, 10));

        await act.Should().NotThrowAsync();
        _vinculoRepo.Verify(r => r.ObterAtivoAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Aluno_AcessaProprioId_Retorna()
    {
        var alunoId = Guid.NewGuid();
        _userContext.Setup(c => c.IsAluno).Returns(true);
        _userContext.Setup(c => c.IsTreinador).Returns(false);
        _userContext.Setup(c => c.PerfilId).Returns(alunoId);
        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Treino>)[], 0));

        var act = async () => await _handler.HandleAsync(new ListarTreinosQuery(alunoId, 1, 10));

        await act.Should().NotThrowAsync();
        _vinculoRepo.Verify(r => r.ObterAtivoAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Aluno_AcessaOutroAluno_LancaAcessoNegadoException()
    {
        var alunoId = Guid.NewGuid();
        _userContext.Setup(c => c.IsAluno).Returns(true);
        _userContext.Setup(c => c.IsTreinador).Returns(false);
        _userContext.Setup(c => c.PerfilId).Returns(Guid.NewGuid());

        var act = async () => await _handler.HandleAsync(new ListarTreinosQuery(alunoId, 1, 10));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_SemTreinos_RetornaListaVazia()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();

        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(VinculoTreinadorAluno.Criar(treinadorId, alunoId));
        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Treino>)[], 0));

        var result = await _handler.HandleAsync(new ListarTreinosQuery(alunoId, 1, 10));

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
