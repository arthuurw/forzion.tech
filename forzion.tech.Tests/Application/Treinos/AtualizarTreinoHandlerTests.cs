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
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<AtualizarTreinoHandler>> _logger = new();
    private readonly AtualizarTreinoHandler _handler;

    public AtualizarTreinoHandlerTests()
    {
        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _handler = new AtualizarTreinoHandler(
            _treinoRepo.Object,
            _unitOfWork.Object,
            _userContext.Object,
            _logger.Object);
    }

    private static Treino CriarTreino(Guid treinadorId) =>
        Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId);

    [Fact]
    public async Task HandleAsync_DadosValidos_AtualizaERetornaResponse()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var command = new AtualizarTreinoCommand(treino.Id, "Novo Nome", ObjetivoTreino.Emagrecimento);
        var result = await _handler.HandleAsync(command);

        result.Nome.Should().Be("Novo Nome");
        result.Objetivo.Should().Be(ObjetivoTreino.Emagrecimento);
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

        result.Nome.Should().Be("Nome Atualizado");
        result.Objetivo.Should().Be(ObjetivoTreino.Hipertrofia);
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

        result.Nome.Should().Be("Treino A");
        result.Objetivo.Should().Be(ObjetivoTreino.Resistencia);
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

        result.Nome.Should().Be("Admin Update");
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
}
