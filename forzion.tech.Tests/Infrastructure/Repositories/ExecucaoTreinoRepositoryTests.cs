using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class ExecucaoTreinoRepositoryTests(InfrastructureTestFixture fixture)
{
    private static ExecucaoTreinoRepository Repo(AppDbContext ctx) => new(ctx);

    /// <summary>
    /// Seeds the minimal graph required for a progressão query:
    /// Treinador → Treino → TreinoExercicio → Exercicio → GrupoMuscular.
    /// Returns IDs needed to build ExecucaoTreino / ExecucaoExercicio rows.
    /// </summary>
    private static async Task<(Guid alunoId, Guid treinoId, Guid treinoExercicioId, string nomeExercicio, string nomeGrupo)>
        SeedProgressaoGraphAsync(AppDbContext ctx, string nomeSuffix = "")
    {
        var agora = DateTime.UtcNow;

        // Conta / Treinador
        var emailTreinador = Email.Criar($"t{Guid.NewGuid():N}@test.com").Value;
        var contaTreinador = Conta.Criar(emailTreinador, "hash", TipoConta.Treinador, agora).Value;
        var treinador = Treinador.Criar(contaTreinador.Id, "Trainer" + nomeSuffix, agora).Value;

        // Conta / Aluno
        var emailAluno = Email.Criar($"a{Guid.NewGuid():N}@test.com").Value;
        var contaAluno = Conta.Criar(emailAluno, "hash", TipoConta.Aluno, agora).Value;
        var aluno = Aluno.Criar(contaAluno.Id, "Aluno" + nomeSuffix, agora).Value;

        await ctx.Contas.AddRangeAsync(contaTreinador, contaAluno);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.SaveChangesAsync();

        // GrupoMuscular
        var nomeGrupo = "Pernas" + nomeSuffix;
        var grupo = GrupoMuscular.Criar(nomeGrupo, agora).Value;
        await ctx.GruposMusculares.AddAsync(grupo);
        await ctx.SaveChangesAsync();

        // Exercicio
        var nomeExercicio = "Agachamento" + nomeSuffix;
        var exercicio = Exercicio.Criar(nomeExercicio, grupo.Id, agora).Value;
        await ctx.Exercicios.AddAsync(exercicio);
        await ctx.SaveChangesAsync();

        // Treino + TreinoExercicio
        var treino = Treino.Criar("Treino" + nomeSuffix, ObjetivoTreino.Forca, treinador.Id, agora).Value;
        var te = treino.AdicionarExercicio(exercicio.Id, agora).Value;
        await ctx.Treinos.AddAsync(treino);
        await ctx.SaveChangesAsync();

        return (aluno.Id, treino.Id, te.Id, nomeExercicio, nomeGrupo);
    }

    [Fact]
    public async Task ProjetarProgressaoAsync_SemExecucoes_RetornaVazio()
    {
        await using var ctx = fixture.CreateContext();
        var (alunoId, _, _, _, _) = await SeedProgressaoGraphAsync(ctx, "Empty");

        var de = new DateTime(2025, 1, 1);
        var ate = new DateTime(2025, 1, 31, 23, 59, 59);

        var result = await Repo(ctx).ProjetarProgressaoAsync(alunoId, de, ate);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ProjetarProgressaoAsync_UmaExecucao_RetornaUmaLinha()
    {
        await using var ctx = fixture.CreateContext();
        var (alunoId, treinoId, treinoExercicioId, nomeExercicio, nomeGrupo) =
            await SeedProgressaoGraphAsync(ctx, "Single");
        var agora = DateTime.UtcNow;
        var dataExecucao = new DateTime(2025, 1, 10, 10, 0, 0, DateTimeKind.Utc);

        var execucao = ExecucaoTreino.Criar(treinoId, alunoId, dataExecucao, agora).Value;
        execucao.AdicionarExercicio(treinoExercicioId, 3, 10, 80m);
        await ctx.ExecucoesTreino.AddAsync(execucao);
        await ctx.SaveChangesAsync();

        var de = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var result = await Repo(ctx).ProjetarProgressaoAsync(alunoId, de, ate);

        result.Should().HaveCount(1);
        result[0].NomeExercicio.Should().Be(nomeExercicio);
        result[0].GrupoMuscular.Should().Be(nomeGrupo);
        result[0].Data.Should().Be(dataExecucao.Date);
        result[0].CargaMaxima.Should().Be(80m);
        result[0].MediaSeries.Should().BeApproximately(3.0, 0.001);
        result[0].MediaRepeticoes.Should().BeApproximately(10.0, 0.001);
    }

    [Fact]
    public async Task ProjetarProgressaoAsync_DoisDias_RetornaDuasLinhas()
    {
        await using var ctx = fixture.CreateContext();
        var (alunoId, treinoId, treinoExercicioId, nomeExercicio, _) =
            await SeedProgressaoGraphAsync(ctx, "TwoDays");
        var agora = DateTime.UtcNow;

        var dia1 = new DateTime(2025, 1, 5, 10, 0, 0, DateTimeKind.Utc);
        var dia2 = new DateTime(2025, 1, 12, 10, 0, 0, DateTimeKind.Utc);

        var e1 = ExecucaoTreino.Criar(treinoId, alunoId, dia1, agora).Value;
        e1.AdicionarExercicio(treinoExercicioId, 4, 12, 80m);
        var e2 = ExecucaoTreino.Criar(treinoId, alunoId, dia2, agora).Value;
        e2.AdicionarExercicio(treinoExercicioId, 4, 12, 85m);
        await ctx.ExecucoesTreino.AddRangeAsync(e1, e2);
        await ctx.SaveChangesAsync();

        var de = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var result = await Repo(ctx).ProjetarProgressaoAsync(alunoId, de, ate);

        result.Should().HaveCount(2);
        result.Should().BeInAscendingOrder(r => r.Data);
        result[0].CargaMaxima.Should().Be(80m);
        result[1].CargaMaxima.Should().Be(85m);
    }

    [Fact]
    public async Task ProjetarProgressaoAsync_MultiplasSeries_AgregaMaxCargaEMedia()
    {
        // Two execucoes on same day → two ExecucaoExercicio rows → MAX(carga), AVG(series), AVG(reps)
        await using var ctx = fixture.CreateContext();
        var (alunoId, treinoId, treinoExercicioId, _, _) =
            await SeedProgressaoGraphAsync(ctx, "Multi");
        var agora = DateTime.UtcNow;
        var dia = new DateTime(2025, 2, 1, 10, 0, 0, DateTimeKind.Utc);

        var e1 = ExecucaoTreino.Criar(treinoId, alunoId, dia, agora).Value;
        e1.AdicionarExercicio(treinoExercicioId, 3, 10, 70m);
        var e2 = ExecucaoTreino.Criar(treinoId, alunoId, dia, agora).Value;
        e2.AdicionarExercicio(treinoExercicioId, 5, 8, 90m);
        await ctx.ExecucoesTreino.AddRangeAsync(e1, e2);
        await ctx.SaveChangesAsync();

        var de = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var ate = new DateTime(2025, 2, 28, 23, 59, 59, DateTimeKind.Utc);

        var result = await Repo(ctx).ProjetarProgressaoAsync(alunoId, de, ate);

        // Both execucoes fall on the same date → grouped into one row
        result.Should().HaveCount(1);
        result[0].CargaMaxima.Should().Be(90m);                         // MAX
        result[0].MediaSeries.Should().BeApproximately(4.0, 0.001);     // AVG(3, 5)
        result[0].MediaRepeticoes.Should().BeApproximately(9.0, 0.001); // AVG(10, 8)
    }

    [Fact]
    public async Task ProjetarProgressaoAsync_FiltroData_ExcluiForaDoIntervalo()
    {
        await using var ctx = fixture.CreateContext();
        var (alunoId, treinoId, treinoExercicioId, _, _) =
            await SeedProgressaoGraphAsync(ctx, "Filter");
        var agora = DateTime.UtcNow;

        var dentro = new DateTime(2025, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var fora = new DateTime(2025, 2, 1, 10, 0, 0, DateTimeKind.Utc);

        var eDentro = ExecucaoTreino.Criar(treinoId, alunoId, dentro, agora).Value;
        eDentro.AdicionarExercicio(treinoExercicioId, 3, 10, 100m);
        var eFora = ExecucaoTreino.Criar(treinoId, alunoId, fora, agora).Value;
        eFora.AdicionarExercicio(treinoExercicioId, 3, 10, 50m);
        await ctx.ExecucoesTreino.AddRangeAsync(eDentro, eFora);
        await ctx.SaveChangesAsync();

        var de = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var ate = new DateTime(2025, 3, 31, 23, 59, 59, DateTimeKind.Utc);

        var result = await Repo(ctx).ProjetarProgressaoAsync(alunoId, de, ate);

        result.Should().HaveCount(1);
        result[0].CargaMaxima.Should().Be(100m);
    }

    [Fact]
    public async Task ProjetarProgressaoAsync_IsolaOutrosAlunos()
    {
        await using var ctx = fixture.CreateContext();
        var (alunoAlvo, treinoId, treinoExercicioId, _, _) =
            await SeedProgressaoGraphAsync(ctx, "Iso1");

        // Seed a second aluno in the same treino
        var emailAluno2 = Email.Criar($"a2{Guid.NewGuid():N}@test.com").Value;
        var conta2 = Conta.Criar(emailAluno2, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        var aluno2 = Aluno.Criar(conta2.Id, "OutroAluno", DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta2);
        await ctx.Alunos.AddAsync(aluno2);
        await ctx.SaveChangesAsync();

        var agora = DateTime.UtcNow;
        var dia = new DateTime(2025, 4, 10, 10, 0, 0, DateTimeKind.Utc);

        var eAlvo = ExecucaoTreino.Criar(treinoId, alunoAlvo, dia, agora).Value;
        eAlvo.AdicionarExercicio(treinoExercicioId, 3, 10, 100m);
        var eOutro = ExecucaoTreino.Criar(treinoId, aluno2.Id, dia, agora).Value;
        eOutro.AdicionarExercicio(treinoExercicioId, 3, 10, 200m);
        await ctx.ExecucoesTreino.AddRangeAsync(eAlvo, eOutro);
        await ctx.SaveChangesAsync();

        var de = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var ate = new DateTime(2025, 4, 30, 23, 59, 59, DateTimeKind.Utc);

        var result = await Repo(ctx).ProjetarProgressaoAsync(alunoAlvo, de, ate);

        result.Should().HaveCount(1);
        result[0].CargaMaxima.Should().Be(100m); // Only alunoAlvo's data
    }

    // ── ANON-02: AnonimizarObservacoesPorAlunoIdAsync ──────────────────────

    [Fact]
    public async Task AnonimizarObservacoesPorAlunoIdAsync_ComObservacao_NullaObservacaoDoAluno()
    {
        await using var ctx = fixture.CreateContext();
        var (alunoId, treinoId, _, _, _) = await SeedProgressaoGraphAsync(ctx, "Anon1");
        var agora = DateTime.UtcNow;

        var e1 = ExecucaoTreino.Criar(treinoId, alunoId, agora, agora, "dados de saúde sensíveis").Value;
        var e2 = ExecucaoTreino.Criar(treinoId, alunoId, agora, agora, "outra observação").Value;
        await ctx.ExecucoesTreino.AddRangeAsync(e1, e2);
        await ctx.SaveChangesAsync();

        await Repo(ctx).AnonimizarObservacoesPorAlunoIdAsync(alunoId);

        await using var ctxVerifica = fixture.CreateContext();
        var execucoes = await ctxVerifica.ExecucoesTreino
            .Where(e => e.AlunoId == alunoId)
            .ToListAsync();

        execucoes.Should().HaveCount(2);
        execucoes.Should().AllSatisfy(e => e.Observacao.Should().BeNull());
    }

    [Fact]
    public async Task AnonimizarObservacoesPorAlunoIdAsync_NaoTocaOutroAluno()
    {
        await using var ctx = fixture.CreateContext();
        var (alunoAlvo, treinoId, _, _, _) = await SeedProgressaoGraphAsync(ctx, "Anon2");
        var agora = DateTime.UtcNow;

        // Seed a second aluno whose execution must remain untouched
        var emailAluno2 = Email.Criar($"anon2b{Guid.NewGuid():N}@test.com").Value;
        var conta2 = Conta.Criar(emailAluno2, "hash", TipoConta.Aluno, agora).Value;
        var aluno2 = Aluno.Criar(conta2.Id, "OutroAlunoAnon", agora).Value;
        await ctx.Contas.AddAsync(conta2);
        await ctx.Alunos.AddAsync(aluno2);
        await ctx.SaveChangesAsync();

        const string obsOutro = "observação do outro aluno";
        var eAlvo = ExecucaoTreino.Criar(treinoId, alunoAlvo, agora, agora, "sensitivo").Value;
        var eOutro = ExecucaoTreino.Criar(treinoId, aluno2.Id, agora, agora, obsOutro).Value;
        await ctx.ExecucoesTreino.AddRangeAsync(eAlvo, eOutro);
        await ctx.SaveChangesAsync();

        await Repo(ctx).AnonimizarObservacoesPorAlunoIdAsync(alunoAlvo);

        await using var ctxVerifica = fixture.CreateContext();
        var execucaoOutro = await ctxVerifica.ExecucoesTreino
            .FirstAsync(e => e.AlunoId == aluno2.Id);

        execucaoOutro.Observacao.Should().Be(obsOutro);
    }
}
