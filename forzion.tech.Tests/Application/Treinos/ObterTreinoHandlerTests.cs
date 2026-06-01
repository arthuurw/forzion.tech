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
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow).Value;
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
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, outroTreinadorId, DateTime.UtcNow).Value;

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
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow).Value;
        var treinoAluno = TreinoAluno.Criar(treino.Id, alunoId, DateTime.UtcNow).Value;

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
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow).Value;

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
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow).Value;

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

    [Fact]
    public async Task HandleAsync_TreinoComExerciciosESeries_ProjetaTodosOsCamposDoResponse()
    {
        var treinadorId = Guid.NewGuid();
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow).Value;
        var exercicioId = Guid.NewGuid();
        var te = treino.AdicionarExercicio(exercicioId, DateTime.UtcNow).Value;
        te.AtualizarObservacao("foco na execução");
        te.AdicionarSerie(quantidade: 4, repeticoesMin: 8, repeticoesMax: 12, descricao: "aquecimento", carga: 60.5m, descanso: 90);

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _exercicioRepo
            .Setup(r => r.ObterNomesPorIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [exercicioId] = "Supino Reto" });

        var result = await _handler.HandleAsync(new ObterTreinoQuery(treino.Id));

        result.Exercicios.Should().HaveCount(1);
        var exResp = result.Exercicios[0];
        exResp.TreinoExercicioId.Should().Be(te.Id);
        exResp.ExercicioId.Should().Be(exercicioId);
        exResp.NomeExercicio.Should().Be("Supino Reto");
        exResp.Ordem.Should().Be(1);
        exResp.Observacao.Should().Be("foco na execução");

        exResp.Series.Should().HaveCount(1);
        var serie = exResp.Series[0];
        serie.SerieConfigId.Should().NotBeEmpty();
        serie.Quantidade.Should().Be(4);
        serie.RepeticoesMin.Should().Be(8);
        serie.RepeticoesMax.Should().Be(12);
        serie.Descricao.Should().Be("aquecimento");
        serie.Carga.Should().Be(60.5m);
        serie.Descanso.Should().Be(90);
        serie.Ordem.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_ExercicioSemNomeMapeado_NomeExercicioVazio()
    {
        var treinadorId = Guid.NewGuid();
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow).Value;
        treino.AdicionarExercicio(Guid.NewGuid(), DateTime.UtcNow);

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var result = await _handler.HandleAsync(new ObterTreinoQuery(treino.Id));

        result.Exercicios.Should().ContainSingle()
            .Which.NomeExercicio.Should().BeEmpty();
    }
}
