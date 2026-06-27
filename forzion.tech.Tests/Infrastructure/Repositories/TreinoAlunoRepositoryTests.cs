using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class TreinoAlunoRepositoryTests(InfrastructureTestFixture fixture)
{
    private static TreinoAlunoRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<Guid> SeedTreinadorAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"t{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        var treinador = Treinador.Criar(conta.Id, "Treinador", DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        return treinador.Id;
    }

    private static async Task<Guid> SeedAlunoAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"a{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        var aluno = Aluno.Criar(conta.Id, "Aluno", DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.SaveChangesAsync();
        return aluno.Id;
    }

    private static async Task<Treino> SeedTreinoAsync(AppDbContext ctx, Guid treinadorId)
    {
        var treino = Treino.Criar($"Treino-{Guid.NewGuid():N}", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow).Value;
        await ctx.Treinos.AddAsync(treino);
        await ctx.SaveChangesAsync();
        return treino;
    }

    private static async Task<TreinoAluno> SeedTreinoAlunoAsync(AppDbContext ctx, Guid treinoId, Guid alunoId, TreinoAlunoStatus status = TreinoAlunoStatus.Ativo)
    {
        var ta = TreinoAluno.Criar(treinoId, alunoId, DateTime.UtcNow).Value;
        if (status == TreinoAlunoStatus.Inativo)
            ta.AlterarStatus(TreinoAlunoStatus.Inativo, DateTime.UtcNow);
        await ctx.TreinoAlunos.AddAsync(ta);
        await ctx.SaveChangesAsync();
        return ta;
    }

    // --- ListarAtivosPorTreinadorAsync ---

    [Fact]
    public async Task ListarAtivosPorTreinadorAsync_RetornaTodosAtivosDeTodosAlunos()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var aluno1Id = await SeedAlunoAsync(ctx);
        var aluno2Id = await SeedAlunoAsync(ctx);
        var treino1 = await SeedTreinoAsync(ctx, treinadorId);
        var treino2 = await SeedTreinoAsync(ctx, treinadorId);

        var ta1 = await SeedTreinoAlunoAsync(ctx, treino1.Id, aluno1Id);
        var ta2 = await SeedTreinoAlunoAsync(ctx, treino2.Id, aluno2Id);

        var result = await Repo(ctx).ListarAtivosPorTreinadorAsync(treinadorId);

        result.Should().HaveCount(2);
        result.Select(ta => ta.Id).Should().BeEquivalentTo(new[] { ta1.Id, ta2.Id });
    }

    [Fact]
    public async Task ListarAtivosPorTreinadorAsync_ExcluiInativosDoMesmoTreinador()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino1 = await SeedTreinoAsync(ctx, treinadorId);
        var treino2 = await SeedTreinoAsync(ctx, treinadorId);

        var taAtivo = await SeedTreinoAlunoAsync(ctx, treino1.Id, alunoId, TreinoAlunoStatus.Ativo);
        await SeedTreinoAlunoAsync(ctx, treino2.Id, alunoId, TreinoAlunoStatus.Inativo);

        var result = await Repo(ctx).ListarAtivosPorTreinadorAsync(treinadorId);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(taAtivo.Id);
    }

    [Fact]
    public async Task ListarAtivosPorTreinadorAsync_IsolaOutrosTreinadores()
    {
        await using var ctx = fixture.CreateContext();
        var treinador1Id = await SeedTreinadorAsync(ctx);
        var treinador2Id = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino1 = await SeedTreinoAsync(ctx, treinador1Id);
        var treino2 = await SeedTreinoAsync(ctx, treinador2Id);

        var taT1 = await SeedTreinoAlunoAsync(ctx, treino1.Id, alunoId);
        await SeedTreinoAlunoAsync(ctx, treino2.Id, alunoId);

        var result = await Repo(ctx).ListarAtivosPorTreinadorAsync(treinador1Id);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(taT1.Id);
    }

    [Fact]
    public async Task ListarAtivosPorTreinadorAsync_TreinadorSemVinculos_RetornaVazio()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);

        var result = await Repo(ctx).ListarAtivosPorTreinadorAsync(treinadorId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListarAtivosPorTreinadorAsync_RetornaEntidadesRastreadas()
    {
        // Ensures rows are EF-tracked (not AsNoTracking) so AlterarStatus persists via SaveChanges
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        await SeedTreinoAlunoAsync(ctx, treino.Id, alunoId);

        var result = await Repo(ctx).ListarAtivosPorTreinadorAsync(treinadorId);

        result.Should().ContainSingle();
        result[0].AlterarStatus(TreinoAlunoStatus.Inativo, DateTime.UtcNow);
        await ctx.SaveChangesAsync();

        var persisted = await ctx.TreinoAlunos.FindAsync(result[0].Id);
        persisted!.Status.Should().Be(TreinoAlunoStatus.Inativo);
    }

    // --- ListarFichasResumoPorAlunoAsync ---

    private static async Task<TreinoAluno> SeedTreinoAlunoEmAsync(
        AppDbContext ctx, Guid treinoId, Guid alunoId, DateTime criadoEm, TreinoAlunoStatus status = TreinoAlunoStatus.Ativo)
    {
        var ta = TreinoAluno.Criar(treinoId, alunoId, criadoEm).Value;
        if (status == TreinoAlunoStatus.Inativo)
            ta.AlterarStatus(TreinoAlunoStatus.Inativo, criadoEm);
        await ctx.TreinoAlunos.AddAsync(ta);
        await ctx.SaveChangesAsync();
        return ta;
    }

    [Fact]
    public async Task ListarFichasResumoPorAlunoAsync_ProjetaCamposRespeitaTakeEOrdenaDesc()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var baseTime = DateTime.UtcNow.AddDays(-10);

        var criadas = new List<(Guid taId, Guid treinoId, string nome, DateTime criadoEm)>();
        for (var i = 0; i < 6; i++)
        {
            var treino = await SeedTreinoAsync(ctx, treinadorId);
            var criadoEm = baseTime.AddMinutes(i);
            var ta = await SeedTreinoAlunoEmAsync(ctx, treino.Id, alunoId, criadoEm);
            criadas.Add((ta.Id, treino.Id, treino.Nome, criadoEm));
        }
        var treinoInativo = await SeedTreinoAsync(ctx, treinadorId);
        await SeedTreinoAlunoEmAsync(ctx, treinoInativo.Id, alunoId, baseTime.AddMinutes(100), TreinoAlunoStatus.Inativo);

        var result = await Repo(ctx).ListarFichasResumoPorAlunoAsync(alunoId, 5);

        result.Should().HaveCount(5);
        var esperadoDesc = criadas.OrderByDescending(c => c.criadoEm).Take(5).Select(c => c.taId).ToList();
        result.Select(f => f.TreinoAlunoId).Should().Equal(esperadoDesc);

        var primeiro = criadas.Single(c => c.taId == result[0].TreinoAlunoId);
        result[0].TreinoId.Should().Be(primeiro.treinoId);
        result[0].NomeTreino.Should().Be(primeiro.nome);
        result[0].Objetivo.Should().Be(ObjetivoTreino.Hipertrofia);
    }

    [Fact]
    public async Task ListarFichasResumoPorAlunoAsync_IgnoraInativosEOutrosAlunos()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var outroAlunoId = await SeedAlunoAsync(ctx);

        var treinoAtivo = await SeedTreinoAsync(ctx, treinadorId);
        var taAtivo = await SeedTreinoAlunoAsync(ctx, treinoAtivo.Id, alunoId, TreinoAlunoStatus.Ativo);

        var treinoInativo = await SeedTreinoAsync(ctx, treinadorId);
        await SeedTreinoAlunoAsync(ctx, treinoInativo.Id, alunoId, TreinoAlunoStatus.Inativo);

        var treinoOutro = await SeedTreinoAsync(ctx, treinadorId);
        await SeedTreinoAlunoAsync(ctx, treinoOutro.Id, outroAlunoId, TreinoAlunoStatus.Ativo);

        var result = await Repo(ctx).ListarFichasResumoPorAlunoAsync(alunoId, 5);

        result.Should().ContainSingle();
        result[0].TreinoAlunoId.Should().Be(taAtivo.Id);
    }
}
