using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.ExcluirTreino;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class ExcluirTreinoHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<ExcluirTreinoHandler>> _logger = new();
    private readonly ExcluirTreinoHandler _handler;

    public ExcluirTreinoHandlerTests()
    {
        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _handler = new ExcluirTreinoHandler(
            _treinoRepo.Object,
            _treinoAlunoRepo.Object,
            _execucaoRepo.Object,
            _unitOfWork.Object,
            _userContext.Object,
            _logger.Object);
    }

    private static Treino CriarTreino(Guid treinadorId) =>
        Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow);

    [Fact]
    public async Task HandleAsync_TreinoValido_RemoveFichasEComita()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoComAlunoAtivoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _handler.HandleAsync(new ExcluirTreinoCommand(treino.Id));

        result.IsSuccess.Should().BeTrue();
        _treinoAlunoRepo.Verify(r => r.RemoverPorTreinoIdAsync(treino.Id, It.IsAny<CancellationToken>()), Times.Once);
        _treinoRepo.Verify(r => r.RemoverAsync(treino, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorDiferente_LancaAcessoNegadoException()
    {
        var treino = CriarTreino(Guid.NewGuid());
        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.PerfilId).Returns(Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var act = async () => await _handler.HandleAsync(new ExcluirTreinoCommand(treino.Id));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_SystemAdmin_PodeExcluirQualquerTreino()
    {
        var treino = CriarTreino(Guid.NewGuid());
        _userContext.Setup(c => c.IsSystemAdmin).Returns(true);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoComAlunoAtivoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = async () => await _handler.HandleAsync(new ExcluirTreinoCommand(treino.Id));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_TreinoJaExecutado_LancaTreinoExecutadoException()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoComAlunoAtivoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = async () => await _handler.HandleAsync(new ExcluirTreinoCommand(treino.Id));

        await act.Should().ThrowAsync<TreinoExecutadoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinoNaoEncontrado_LancaTreinoNaoEncontradoException()
    {
        var treinoId = Guid.NewGuid();
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treinoId, It.IsAny<CancellationToken>())).ReturnsAsync((Treino?)null);

        var act = async () => await _handler.HandleAsync(new ExcluirTreinoCommand(treinoId));
        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinoExecutado_NaoRemoveFichasNemComita()
    {
        var treinadorId = Guid.NewGuid();
        var treino = CriarTreino(treinadorId);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _execucaoRepo.Setup(r => r.ExisteParaTreinoComAlunoAtivoAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = async () => await _handler.HandleAsync(new ExcluirTreinoCommand(treino.Id));

        await act.Should().ThrowAsync<TreinoExecutadoException>();
        _treinoAlunoRepo.Verify(r => r.RemoverPorTreinoIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
