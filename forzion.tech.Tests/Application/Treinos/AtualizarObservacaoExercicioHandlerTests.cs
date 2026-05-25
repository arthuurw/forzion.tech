using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.AtualizarObservacaoExercicio;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class AtualizarObservacaoExercicioHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<AtualizarObservacaoExercicioHandler>> _logger = new();
    private readonly AtualizarObservacaoExercicioHandler _handler;

    public AtualizarObservacaoExercicioHandlerTests()
    {
        _userContext.Setup(c => c.IsSystemAdmin).Returns(true);
        _exercicioRepo
            .Setup(r => r.ObterNomesPorIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _handler = new AtualizarObservacaoExercicioHandler(
            _treinoRepo.Object, _exercicioRepo.Object, _unitOfWork.Object, _userContext.Object, _logger.Object);
    }

    private static (Treino treino, TreinoExercicio exercicio) CriarTreinoComExercicio(Guid treinadorId)
    {
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow);
        var ex = treino.AdicionarExercicio(Guid.NewGuid());
        ex.AdicionarSerie(3, 10, 12, null, null, null);
        return (treino, ex);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_AtualizaObservacaoERetorna()
    {
        var treinadorId = Guid.NewGuid();
        var (treino, ex) = CriarTreinoComExercicio(treinadorId);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var command = new AtualizarObservacaoExercicioCommand(treino.Id, ex.Id, "Manter cotovelo fixo");
        var result = await _handler.HandleAsync(command);

        result.Exercicios[0].Observacao.Should().Be("Manter cotovelo fixo");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ObservacaoNula_LimpaObservacao()
    {
        var treinadorId = Guid.NewGuid();
        var (treino, ex) = CriarTreinoComExercicio(treinadorId);
        ex.AtualizarObservacao("observação anterior");
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var command = new AtualizarObservacaoExercicioCommand(treino.Id, ex.Id, null);
        var result = await _handler.HandleAsync(command);

        result.Exercicios[0].Observacao.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_ObservacaoVazia_LimpaObservacao()
    {
        var treinadorId = Guid.NewGuid();
        var (treino, ex) = CriarTreinoComExercicio(treinadorId);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var command = new AtualizarObservacaoExercicioCommand(treino.Id, ex.Id, "   ");
        var result = await _handler.HandleAsync(command);

        result.Exercicios[0].Observacao.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_ObservacaoMuitoLonga_LancaDomainException()
    {
        var (treino, ex) = CriarTreinoComExercicio(Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var command = new AtualizarObservacaoExercicioCommand(treino.Id, ex.Id, new string('x', 501));
        var act = async () => await _handler.HandleAsync(command);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorDiferente_LancaAcessoNegadoException()
    {
        var (treino, ex) = CriarTreinoComExercicio(Guid.NewGuid());
        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.PerfilId).Returns(Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var act = async () => await _handler.HandleAsync(
            new AtualizarObservacaoExercicioCommand(treino.Id, ex.Id, "obs"));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_SystemAdmin_PodeAtualizar()
    {
        var (treino, ex) = CriarTreinoComExercicio(Guid.NewGuid());
        _userContext.Setup(c => c.IsSystemAdmin).Returns(true);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var result = await _handler.HandleAsync(
            new AtualizarObservacaoExercicioCommand(treino.Id, ex.Id, "foco na contração"));

        result.Exercicios[0].Observacao.Should().Be("foco na contração");
    }

    [Fact]
    public async Task HandleAsync_TreinoNaoEncontrado_LancaTreinoNaoEncontradoException()
    {
        _treinoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treino?)null);

        var act = async () => await _handler.HandleAsync(
            new AtualizarObservacaoExercicioCommand(Guid.NewGuid(), Guid.NewGuid(), "obs"));

        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_ExercicioNaoEncontrado_LancaTreinoNaoEncontradoException()
    {
        var (treino, _) = CriarTreinoComExercicio(Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var act = async () => await _handler.HandleAsync(
            new AtualizarObservacaoExercicioCommand(treino.Id, Guid.NewGuid(), "obs"));

        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
