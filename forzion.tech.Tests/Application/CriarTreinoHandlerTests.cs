using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.CriarTreino;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class CriarTreinoHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<CriarTreinoHandler>> _logger = new();
    private readonly CriarTreinoCommandValidator _validator = new();
    private readonly CriarTreinoHandler _handler;

    public CriarTreinoHandlerTests()
    {
        _userContext.Setup(c => c.IsSystemAdmin).Returns(true);
        _handler = new CriarTreinoHandler(
            _treinoRepo.Object,
            _treinoAlunoRepo.Object,
            _alunoRepo.Object,
            _vinculoRepo.Object,
            _unitOfWork.Object,
            _userContext.Object,
            _validator, TimeProvider.System,
            _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CadastraERetorna()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var aluno = Aluno.Criar(alunoId, "João", DateTime.UtcNow);

        _alunoRepo.Setup(r => r.ObterPorIdAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var command = new CriarTreinoCommand(treinadorId, alunoId, "Treino A", ObjetivoTreino.Hipertrofia);
        var result = await _handler.HandleAsync(command);

        result.Nome.Should().Be("Treino A");
        result.TreinadorId.Should().Be(treinadorId);
        _treinoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Treino>(), It.IsAny<CancellationToken>()), Times.Once);
        _treinoAlunoRepo.Verify(r => r.AdicionarAsync(It.IsAny<TreinoAluno>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AcessoNegado_LancaAcessoNegadoException()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var aluno = Aluno.Criar(alunoId, "João", DateTime.UtcNow);

        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        var command = new CriarTreinoCommand(treinadorId, alunoId, "Treino A", ObjetivoTreino.Hipertrofia);
        var act = async () => await _handler.HandleAsync(command);

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_LancaAlunoNaoEncontradoException()
    {
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        var command = new CriarTreinoCommand(Guid.NewGuid(), Guid.NewGuid(), "Treino A", ObjetivoTreino.Hipertrofia);
        var act = async () => await _handler.HandleAsync(command);

        await act.Should().ThrowAsync<AlunoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_DadosInvalidos_LancaValidationException()
    {
        var command = new CriarTreinoCommand(Guid.NewGuid(), Guid.Empty, "", (ObjetivoTreino)99);
        var act = async () => await _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_SemAlunoId_CriaTreinoSemVincularAluno()
    {
        var treinadorId = Guid.NewGuid();
        var command = new CriarTreinoCommand(treinadorId, null, "Treino Livre", ObjetivoTreino.Forca);

        var result = await _handler.HandleAsync(command);

        result.Nome.Should().Be("Treino Livre");
        _alunoRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _treinoAlunoRepo.Verify(r => r.AdicionarAsync(It.IsAny<TreinoAluno>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ComDificuldadeEDatas_AtribuiCamposNaResposta()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var aluno = Aluno.Criar(alunoId, "João", DateTime.UtcNow);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var inicio = new DateOnly(2025, 1, 1);
        var fim = new DateOnly(2025, 6, 30);
        var command = new CriarTreinoCommand(treinadorId, alunoId, "Treino Avançado", ObjetivoTreino.Hipertrofia,
            DificuldadeTreino.Avancado, inicio, fim);

        var result = await _handler.HandleAsync(command);

        result.Dificuldade.Should().Be(DificuldadeTreino.Avancado);
        result.DataInicio.Should().Be(inicio);
        result.DataFim.Should().Be(fim);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
