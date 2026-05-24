using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.ListarAlunosTreino;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class ListarAlunosTreinoHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly ListarAlunosTreinoHandler _handler;

    public ListarAlunosTreinoHandlerTests()
    {
        _userContext.Setup(u => u.IsSystemAdmin).Returns(false);
        _handler = new ListarAlunosTreinoHandler(_treinoRepo.Object, _treinoAlunoRepo.Object, _userContext.Object);
    }

    private static Treino CriarTreino(Guid treinadorId) =>
        Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow);

    [Fact]
    public async Task HandleAsync_TreinadorDono_RetornaAlunos()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        var alunos = new List<TreinoAlunoVinculado>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "João", TreinoAlunoStatus.Ativo)
        };

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _treinoAlunoRepo.Setup(r => r.ListarAtivosPorTreinoIdAsync(treino.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alunos);

        var result = await _handler.HandleAsync(new ListarAlunosTreinoCommand(treino.Id));

        result.Should().HaveCount(1);
        result[0].NomeAluno.Should().Be("João");
    }

    [Fact]
    public async Task HandleAsync_TreinadorDiferente_LancaAcessoNegadoException()
    {
        var treino = CriarTreino(Guid.NewGuid());
        _userContext.Setup(u => u.PerfilId).Returns(Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var act = async () => await _handler.HandleAsync(new ListarAlunosTreinoCommand(treino.Id));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_AdminQualquerTreino_RetornaAlunos()
    {
        var treino = CriarTreino(Guid.NewGuid());
        _userContext.Setup(u => u.IsSystemAdmin).Returns(true);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _treinoAlunoRepo.Setup(r => r.ListarAtivosPorTreinoIdAsync(treino.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TreinoAlunoVinculado>());

        var result = await _handler.HandleAsync(new ListarAlunosTreinoCommand(treino.Id));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_TreinoNaoEncontrado_LancaTreinoNaoEncontradoException()
    {
        _treinoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treino?)null);

        var act = async () => await _handler.HandleAsync(new ListarAlunosTreinoCommand(Guid.NewGuid()));

        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
