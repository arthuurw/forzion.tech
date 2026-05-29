using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Alunos;

public class ObterProgressaoAlunoHandlerTests
{
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly ObterProgressaoAlunoHandler _handler;

    public ObterProgressaoAlunoHandlerTests()
    {
        _handler = new ObterProgressaoAlunoHandler(_execucaoRepo.Object, _vinculoRepo.Object, _userContext.Object);
    }

    private static VinculoTreinadorAluno CriarVinculo(Guid treinadorId, Guid alunoId)
    {
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, alunoId, DateTime.UtcNow, Guid.NewGuid()).Value;
        return vinculo;
    }

    [Fact]
    public async Task HandleAsync_VinculoAtivo_RetornaProgressao()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var de = new DateTime(2025, 1, 1);
        var ate = new DateTime(2025, 1, 31);
        var vinculo = CriarVinculo(treinadorId, alunoId);

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);
        _execucaoRepo.Setup(r => r.ProjetarProgressaoAsync(
                alunoId, de.Date, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProgressaoAggRow>());

        var result = await _handler.HandleAsync(new ObterProgressaoAlunoQuery(alunoId, de, ate));

        result.Exercicios.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_SemVinculoAtivo_LancaAcessoNegadoException()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        var act = async () => await _handler.HandleAsync(
            new ObterProgressaoAlunoQuery(alunoId, DateTime.Today.AddDays(-7), DateTime.Today));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_ComExerciciosNoHistorico_ProjetaCorretamente()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var de = new DateTime(2025, 1, 1);
        var ate = new DateTime(2025, 1, 31);
        var vinculo = CriarVinculo(treinadorId, alunoId);

        // SQL-aggregated row: one entry per (exercício, grupoMuscular, data)
        var row = new ProgressaoAggRow("Agachamento", "Pernas", de.Date, 100m, 4.0, 12.0);

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);
        _execucaoRepo.Setup(r => r.ProjetarProgressaoAsync(
                alunoId, de.Date, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProgressaoAggRow> { row });

        var result = await _handler.HandleAsync(new ObterProgressaoAlunoQuery(alunoId, de, ate));

        result.Exercicios.Should().HaveCount(1);
        result.Exercicios[0].NomeExercicio.Should().Be("Agachamento");
        result.Exercicios[0].GrupoMuscular.Should().Be("Pernas");
        result.Exercicios[0].Historico.Should().HaveCount(1);
        result.Exercicios[0].Historico[0].CargaMaxima.Should().Be(100m);
        result.Exercicios[0].Historico[0].SeriesExecutadas.Should().Be(4);
        result.Exercicios[0].Historico[0].RepeticoesExecutadas.Should().Be(12);
    }

    [Fact]
    public async Task HandleAsync_MultiplosDias_HistoricoOrdenadoPorData()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var de = new DateTime(2025, 1, 1);
        var ate = new DateTime(2025, 1, 31);
        var vinculo = CriarVinculo(treinadorId, alunoId);

        // Two days for the same exercise — SQL already returns them ordered by data
        var rows = new List<ProgressaoAggRow>
        {
            new("Supino", "Peito", new DateTime(2025, 1, 5), 80m, 3.0, 10.0),
            new("Supino", "Peito", new DateTime(2025, 1, 12), 85m, 4.0, 8.0),
        };

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);
        _execucaoRepo.Setup(r => r.ProjetarProgressaoAsync(
                alunoId, de.Date, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var result = await _handler.HandleAsync(new ObterProgressaoAlunoQuery(alunoId, de, ate));

        result.Exercicios.Should().HaveCount(1);
        result.Exercicios[0].NomeExercicio.Should().Be("Supino");
        result.Exercicios[0].Historico.Should().HaveCount(2);
        result.Exercicios[0].Historico[0].CargaMaxima.Should().Be(80m);
        result.Exercicios[0].Historico[1].CargaMaxima.Should().Be(85m);
    }

    [Fact]
    public async Task HandleAsync_MultiplosExercicios_OrdenadoPorGrupoEExercicio()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var de = new DateTime(2025, 1, 1);
        var ate = new DateTime(2025, 1, 31);
        var vinculo = CriarVinculo(treinadorId, alunoId);
        var dia = new DateTime(2025, 1, 10);

        // SQL returns them already ordered by grupo/exercicio/data
        var rows = new List<ProgressaoAggRow>
        {
            new("Agachamento", "Pernas", dia, 100m, 4.0, 12.0),
            new("Supino", "Peito", dia, 80m, 3.0, 10.0),
        };

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);
        _execucaoRepo.Setup(r => r.ProjetarProgressaoAsync(
                alunoId, de.Date, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var result = await _handler.HandleAsync(new ObterProgressaoAlunoQuery(alunoId, de, ate));

        result.Exercicios.Should().HaveCount(2);
        // OrderBy grupoMuscular: Peito < Pernas
        result.Exercicios[0].NomeExercicio.Should().Be("Supino");
        result.Exercicios[1].NomeExercicio.Should().Be("Agachamento");
    }

    [Fact]
    public async Task HandleAsync_CargaNula_CargaMaximaRetornaNula()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var de = new DateTime(2025, 1, 1);
        var ate = new DateTime(2025, 1, 31);
        var vinculo = CriarVinculo(treinadorId, alunoId);

        var row = new ProgressaoAggRow("Flexão", "Peito", de.Date, null, 3.0, 15.0);

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);
        _execucaoRepo.Setup(r => r.ProjetarProgressaoAsync(
                alunoId, de.Date, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProgressaoAggRow> { row });

        var result = await _handler.HandleAsync(new ObterProgressaoAlunoQuery(alunoId, de, ate));

        result.Exercicios[0].Historico[0].CargaMaxima.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_SystemAdmin_IgnoraVerificacaoVinculo()
    {
        var alunoId = Guid.NewGuid();
        var de = new DateTime(2025, 1, 1);
        var ate = new DateTime(2025, 1, 31);

        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.SystemAdmin);
        _userContext.Setup(u => u.IsSystemAdmin).Returns(true);
        _execucaoRepo.Setup(r => r.ProjetarProgressaoAsync(
                alunoId, de.Date, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProgressaoAggRow>());

        var result = await _handler.HandleAsync(new ObterProgressaoAlunoQuery(alunoId, de, ate));

        result.Exercicios.Should().BeEmpty();
        _vinculoRepo.Verify(r => r.ObterAtivoAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
