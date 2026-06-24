using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Exercicios.ExcluirExercicio;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Exercicios;

public class ExcluirExercicioHandlerTests
{
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly ExcluirExercicioHandler _handler;

    private static readonly Guid AtorId = Guid.NewGuid();

    public ExcluirExercicioHandlerTests()
    {
        _userContext.Setup(u => u.PerfilId).Returns(AtorId);
        _handler = new ExcluirExercicioHandler(
            _exercicioRepo.Object,
            _unitOfWork.Object,
            _logRepo.Object,
            Mock.Of<ILogger<ExcluirExercicioHandler>>(),
            _timeProvider,
            _userContext.Object);
    }

    private static readonly Guid GrupoId = Guid.NewGuid();

    private static Exercicio CriarExercicioTreinador(Guid treinadorId) =>
        Exercicio.Criar("Supino Reto", GrupoId, DateTime.UtcNow, treinadorId).Value;

    private static Exercicio CriarExercicioGlobal() =>
        Exercicio.Criar("Agachamento", GrupoId, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_TreinadorExcluiProprio_RemoveEComita()
    {
        var treinadorId = Guid.NewGuid();
        var exercicio = CriarExercicioTreinador(treinadorId);
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);
        _exercicioRepo.Setup(r => r.EstaEmUsoAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await _handler.HandleAsync(new ExcluirExercicioCommand(exercicio.Id, treinadorId));

        _exercicioRepo.Verify(r => r.RemoverAsync(exercicio, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorExcluiProprio_RegistraLogAuditoria()
    {
        var treinadorId = Guid.NewGuid();
        var exercicio = CriarExercicioTreinador(treinadorId);
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);
        _exercicioRepo.Setup(r => r.EstaEmUsoAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await _handler.HandleAsync(new ExcluirExercicioCommand(exercicio.Id, treinadorId));

        _logRepo.Verify(r => r.AdicionarAsync(
            It.Is<LogAprovacao>(l => l.TipoAcao == TipoAcaoAprovacao.ExclusaoExercicio),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AdminExcluiGlobal_RemoveEComita()
    {
        var exercicio = CriarExercicioGlobal();
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);
        _exercicioRepo.Setup(r => r.EstaEmUsoAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await _handler.HandleAsync(new ExcluirExercicioCommand(exercicio.Id, null));

        _exercicioRepo.Verify(r => r.RemoverAsync(exercicio, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ExercicioNaoEncontrado_LancaExercicioNaoEncontradoException()
    {
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Exercicio?)null);

        var act = async () => await _handler.HandleAsync(new ExcluirExercicioCommand(Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<ExercicioNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorExcluiDeOutro_LancaAcessoNegadoException()
    {
        var exercicio = CriarExercicioTreinador(Guid.NewGuid());
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);

        var act = async () => await _handler.HandleAsync(new ExcluirExercicioCommand(exercicio.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_AdminExcluiNaoGlobal_LancaAcessoNegadoException()
    {
        var exercicio = CriarExercicioTreinador(Guid.NewGuid());
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);

        var act = async () => await _handler.HandleAsync(new ExcluirExercicioCommand(exercicio.Id, null));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_ExercicioEmUso_RetornaFalha()
    {
        var treinadorId = Guid.NewGuid();
        var exercicio = CriarExercicioTreinador(treinadorId);
        _exercicioRepo.Setup(r => r.ObterPorIdAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(exercicio);
        _exercicioRepo.Setup(r => r.EstaEmUsoAsync(exercicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _handler.HandleAsync(new ExcluirExercicioCommand(exercicio.Id, treinadorId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("em uso");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
