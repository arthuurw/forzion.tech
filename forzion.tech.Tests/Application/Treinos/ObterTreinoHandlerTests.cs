using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.ObterTreino;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class ObterTreinoHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<ObterTreinoHandler>> _logger = new();
    private readonly ObterTreinoHandler _handler;

    public ObterTreinoHandlerTests()
    {
        _exercicioRepo
            .Setup(r => r.ObterNomesPorIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _handler = new ObterTreinoHandler(
            _treinoRepo.Object, _exercicioRepo.Object, _treinoAlunoRepo.Object, _userContext.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_TreinoExistente_RetornaTreino()
    {
        var treinadorId = Guid.NewGuid();
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow);
        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var result = await _handler.HandleAsync(new ObterTreinoQuery(treino.Id));

        result.TreinoId.Should().Be(treino.Id);
        result.Nome.Should().Be("Treino A");
    }

    [Fact]
    public async Task HandleAsync_TreinoDeOutroTreinador_LancaAcessoNegadoException()
    {
        var treinadorLogadoId = Guid.NewGuid();
        var outroTreinadorId = Guid.NewGuid();
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, outroTreinadorId, DateTime.UtcNow);

        _userContext.Setup(u => u.PerfilId).Returns(treinadorLogadoId);
        _userContext.Setup(u => u.IsTreinador).Returns(true);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var act = async () => await _handler.HandleAsync(new ObterTreinoQuery(treino.Id));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinoNaoEncontrado_LancaTreinoNaoEncontradoException()
    {
        var treinoId = Guid.NewGuid();
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treinoId, It.IsAny<CancellationToken>())).ReturnsAsync((Treino?)null);

        var act = async () => await _handler.HandleAsync(new ObterTreinoQuery(treinoId));

        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_AlunoComTreinoVinculado_RetornaTreino()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow);
        var treinoAluno = TreinoAluno.Criar(treino.Id, alunoId, DateTime.UtcNow);

        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _userContext.Setup(u => u.IsSystemAdmin).Returns(false);
        _userContext.Setup(u => u.IsAluno).Returns(true);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _treinoAlunoRepo.Setup(r => r.ObterAsync(treino.Id, alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(treinoAluno);

        var result = await _handler.HandleAsync(new ObterTreinoQuery(treino.Id));

        result.TreinoId.Should().Be(treino.Id);
    }

    [Fact]
    public async Task HandleAsync_AlunoSemVinculoAoTreino_LancaAcessoNegadoException()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow);

        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _userContext.Setup(u => u.IsSystemAdmin).Returns(false);
        _userContext.Setup(u => u.IsAluno).Returns(true);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _treinoAlunoRepo.Setup(r => r.ObterAsync(treino.Id, alunoId, It.IsAny<CancellationToken>())).ReturnsAsync((TreinoAluno?)null);

        var act = async () => await _handler.HandleAsync(new ObterTreinoQuery(treino.Id));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_SystemAdmin_AcessaTreinoDeQualquerTreinador()
    {
        var treinadorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow);

        _userContext.Setup(u => u.PerfilId).Returns(adminId);
        _userContext.Setup(u => u.IsSystemAdmin).Returns(true);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var result = await _handler.HandleAsync(new ObterTreinoQuery(treino.Id));

        result.TreinoId.Should().Be(treino.Id);
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
