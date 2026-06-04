using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.AtualizarTreino;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class AtualizarTreinoHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<AtualizarTreinoHandler>> _logger = new();
    private readonly AtualizarTreinoHandler _handler;

    public AtualizarTreinoHandlerTests()
    {
        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _execucaoRepo
            .Setup(r => r.ExisteParaTreinoComAlunoAtivoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _exercicioRepo
            .Setup(r => r.ObterNomesPorIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _handler = new AtualizarTreinoHandler(
            _treinoRepo.Object,
            _exercicioRepo.Object,
            _execucaoRepo.Object,
            _unitOfWork.Object,
            _userContext.Object,
            TimeProvider.System,
            _logger.Object);
    }

    private static Treino CriarTreino(Guid treinadorId) =>
        Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_DadosValidos_AtualizaERetornaResponse()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var command = new AtualizarTreinoCommand(treino.Id, "Novo Nome", ObjetivoTreino.Emagrecimento);
        var result = await _handler.HandleAsync(command);

        result.Value.Nome.Should().Be("Novo Nome");
        result.Value.Objetivo.Should().Be(ObjetivoTreino.Emagrecimento);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ApenasNome_AtualizaSoNome()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var command = new AtualizarTreinoCommand(treino.Id, "Nome Atualizado", null);
        var result = await _handler.HandleAsync(command);

        result.Value.Nome.Should().Be("Nome Atualizado");
        result.Value.Objetivo.Should().Be(ObjetivoTreino.Hipertrofia);
    }

    [Fact]
    public async Task HandleAsync_ApenasObjetivo_AtualizaSoObjetivo()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var command = new AtualizarTreinoCommand(treino.Id, null, ObjetivoTreino.Resistencia);
        var result = await _handler.HandleAsync(command);

        result.Value.Nome.Should().Be("Treino A");
        result.Value.Objetivo.Should().Be(ObjetivoTreino.Resistencia);
    }

    [Fact]
    public async Task HandleAsync_TreinadorDiferente_LancaAcessoNegadoException()
    {
        var treino = CriarTreino(Guid.NewGuid());
        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.PerfilId).Returns(Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var act = async () => await _handler.HandleAsync(new AtualizarTreinoCommand(treino.Id, "X", null));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_SystemAdmin_PodeAtualizarQualquerTreino()
    {
        var treino = CriarTreino(Guid.NewGuid());
        _userContext.Setup(c => c.IsSystemAdmin).Returns(true);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var command = new AtualizarTreinoCommand(treino.Id, "Admin Update", null);
        var result = await _handler.HandleAsync(command);

        result.Value.Nome.Should().Be("Admin Update");
    }

    [Fact]
    public async Task HandleAsync_TreinoNaoEncontrado_LancaTreinoNaoEncontradoException()
    {
        var treinoId = Guid.NewGuid();
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treinoId, It.IsAny<CancellationToken>())).ReturnsAsync((Treino?)null);

        var act = async () => await _handler.HandleAsync(new AtualizarTreinoCommand(treinoId, "X", null));
        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_AtualizaDificuldade_RetornaComNovaDificuldade()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var command = new AtualizarTreinoCommand(treino.Id, null, null, DificuldadeTreino.Avancado);
        var result = await _handler.HandleAsync(command);

        result.Value.Dificuldade.Should().Be(DificuldadeTreino.Avancado);
    }

    [Fact]
    public async Task HandleAsync_AtualizaDatas_RetornaComDatas()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var inicio = new DateOnly(2025, 1, 1);
        var fim = new DateOnly(2025, 12, 31);
        var command = new AtualizarTreinoCommand(treino.Id, null, null, DataInicio: inicio, DataFim: fim);
        var result = await _handler.HandleAsync(command);

        result.Value.DataInicio.Should().Be(inicio);
        result.Value.DataFim.Should().Be(fim);
    }

    [Fact]
    public async Task HandleAsync_TreinoJaExecutado_RetornaFailureMutabilidade()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoComAlunoAtivoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var command = new AtualizarTreinoCommand(treino.Id, "Novo Nome", ObjetivoTreino.Emagrecimento);
        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("treino.ja_executado");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinoSemExecucao_AtualizaComSucesso()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoComAlunoAtivoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var command = new AtualizarTreinoCommand(treino.Id, "Novo Nome", ObjetivoTreino.Emagrecimento);
        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.Nome.Should().Be("Novo Nome");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_LimparDatas_RetornaSemDatas()
    {
        var treinadorId = Guid.NewGuid();
        var treino = Treino.Criar("T", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow,
            dataInicio: new DateOnly(2025, 1, 1),
            dataFim: new DateOnly(2025, 12, 31)).Value;
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var command = new AtualizarTreinoCommand(treino.Id, null, null, LimparDataInicio: true, LimparDataFim: true);
        var result = await _handler.HandleAsync(command);

        result.Value.DataInicio.Should().BeNull();
        result.Value.DataFim.Should().BeNull();
    }
}
