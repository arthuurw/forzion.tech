using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;

namespace forzion.tech.Tests.Application.Alunos;

/// <summary>
/// Cobre <see cref="ProgressaoProjection.Projetar"/> — projeção in-memory a partir de
/// execuções hidratadas (<see cref="ExecucaoDetalheItem"/>). Diferente do caminho SQL-agregado
/// do handler, este método agrupa por (exercício, grupo) e por dia, calcula carga máxima e
/// médias de séries/repetições. Também exercita o getter <c>PontoProgressao.Data</c>.
/// </summary>
public class ProgressaoProjectionTests
{
    private static ExecucaoDetalheItem Execucao(DateTime data, params ExecucaoExercicioDetalhe[] exercicios) =>
        new(Guid.NewGuid(), data, Guid.NewGuid(), null, exercicios);

    [Fact]
    public void Projetar_SemExecucoes_RetornaListaVazia()
    {
        var resultado = ProgressaoProjection.Projetar(new List<ExecucaoDetalheItem>());

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Projetar_UmaExecucao_ProjetaExercicioEPontoComData()
    {
        var data = new DateTime(2026, 1, 10, 8, 30, 0, DateTimeKind.Utc);
        var execucoes = new List<ExecucaoDetalheItem>
        {
            Execucao(data, new ExecucaoExercicioDetalhe(Guid.NewGuid(), "Agachamento", "Pernas", 4, 12, 100m)),
        };

        var resultado = ProgressaoProjection.Projetar(execucoes);

        resultado.Should().HaveCount(1);
        resultado[0].NomeExercicio.Should().Be("Agachamento");
        resultado[0].GrupoMuscular.Should().Be("Pernas");
        resultado[0].Historico.Should().HaveCount(1);
        var ponto = resultado[0].Historico[0];
        ponto.Data.Should().Be(data.Date);
        ponto.CargaMaxima.Should().Be(100m);
        ponto.SeriesExecutadas.Should().Be(4);
        ponto.RepeticoesExecutadas.Should().Be(12);
    }

    [Fact]
    public void Projetar_MesmoDiaMultiplosRegistros_AgregaCargaMaximaEMedias()
    {
        var data = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var exercicioId = Guid.NewGuid();
        var execucoes = new List<ExecucaoDetalheItem>
        {
            Execucao(data, new ExecucaoExercicioDetalhe(exercicioId, "Supino", "Peito", 3, 10, 80m)),
            Execucao(data, new ExecucaoExercicioDetalhe(exercicioId, "Supino", "Peito", 5, 12, 90m)),
        };

        var resultado = ProgressaoProjection.Projetar(execucoes);

        resultado.Should().HaveCount(1);
        resultado[0].Historico.Should().HaveCount(1);
        var ponto = resultado[0].Historico[0];
        ponto.CargaMaxima.Should().Be(90m);   // Max(80, 90)
        ponto.SeriesExecutadas.Should().Be(4); // Round(avg(3,5))
        ponto.RepeticoesExecutadas.Should().Be(11); // Round(avg(10,12))
    }

    [Fact]
    public void Projetar_DiasDiferentes_HistoricoOrdenadoPorData()
    {
        var dia1 = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var dia2 = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc);
        var execucoes = new List<ExecucaoDetalheItem>
        {
            Execucao(dia2, new ExecucaoExercicioDetalhe(Guid.NewGuid(), "Remada", "Costas", 4, 10, 60m)),
            Execucao(dia1, new ExecucaoExercicioDetalhe(Guid.NewGuid(), "Remada", "Costas", 4, 10, 55m)),
        };

        var resultado = ProgressaoProjection.Projetar(execucoes);

        resultado.Should().HaveCount(1);
        resultado[0].Historico.Should().HaveCount(2);
        resultado[0].Historico[0].Data.Should().Be(dia1.Date);
        resultado[0].Historico[1].Data.Should().Be(dia2.Date);
    }

    [Fact]
    public void Projetar_MultiplosExercicios_OrdenadoPorGrupoEExercicio()
    {
        var data = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var execucoes = new List<ExecucaoDetalheItem>
        {
            Execucao(data,
                new ExecucaoExercicioDetalhe(Guid.NewGuid(), "Agachamento", "Pernas", 4, 12, 100m),
                new ExecucaoExercicioDetalhe(Guid.NewGuid(), "Supino", "Peito", 3, 10, 80m)),
        };

        var resultado = ProgressaoProjection.Projetar(execucoes);

        resultado.Should().HaveCount(2);
        // OrderBy grupoMuscular (Peito < Pernas), depois nomeExercicio
        resultado[0].GrupoMuscular.Should().Be("Peito");
        resultado[0].NomeExercicio.Should().Be("Supino");
        resultado[1].GrupoMuscular.Should().Be("Pernas");
        resultado[1].NomeExercicio.Should().Be("Agachamento");
    }

    [Fact]
    public void Projetar_CargaNula_PreservaNull()
    {
        var data = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var execucoes = new List<ExecucaoDetalheItem>
        {
            Execucao(data, new ExecucaoExercicioDetalhe(Guid.NewGuid(), "Flexão", "Peito", 3, 15, null)),
        };

        var resultado = ProgressaoProjection.Projetar(execucoes);

        resultado[0].Historico[0].CargaMaxima.Should().BeNull();
    }
}
