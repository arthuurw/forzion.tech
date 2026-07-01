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

    // --- Seeding helpers: Exercicio/Serie ---

    private static async Task<Guid> SeedExercicioAsync(AppDbContext ctx)
    {
        var grupo = GrupoMuscular.Criar($"G-{Guid.NewGuid():N}", DateTime.UtcNow).Value;
        await ctx.GruposMusculares.AddAsync(grupo);
        var ex = Exercicio.Criar($"E-{Guid.NewGuid():N}", grupo.Id, DateTime.UtcNow).Value;
        await ctx.Exercicios.AddAsync(ex);
        await ctx.SaveChangesAsync();
        return ex.Id;
    }

    private static async Task<Treino> SeedTreinoComExercicioESerieAsync(AppDbContext ctx, Guid treinadorId)
    {
        var exercicioId = await SeedExercicioAsync(ctx);
        var treino = Treino.Criar($"Treino-{Guid.NewGuid():N}", ObjetivoTreino.Hipertrofia, treinadorId, DateTime.UtcNow).Value;
        var te = treino.AdicionarExercicio(exercicioId, DateTime.UtcNow).Value;
        te.AdicionarSerie(3, 8, 12, null, 50m, 60);
        await ctx.Treinos.AddAsync(treino);
        await ctx.SaveChangesAsync();
        return treino;
    }

    // --- ObterAsync ---

    [Fact]
    public async Task ObterAsync_ParExistente_RetornaEntidade()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        var ta = await SeedTreinoAlunoAsync(ctx, treino.Id, alunoId);

        var result = await Repo(ctx).ObterAsync(treino.Id, alunoId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(ta.Id);
    }

    [Fact]
    public async Task ObterAsync_ParInexistente_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);

        var result = await Repo(ctx).ObterAsync(treino.Id, alunoId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ObterAsync_NaoFiltraPorStatus_RetornaInativo()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        var ta = await SeedTreinoAlunoAsync(ctx, treino.Id, alunoId, TreinoAlunoStatus.Inativo);

        var result = await Repo(ctx).ObterAsync(treino.Id, alunoId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(ta.Id);
    }

    // --- ContarAtivosPorAlunoAsync ---

    [Fact]
    public async Task ContarAtivosPorAlunoAsync_ContaSomenteAtivosDoAluno()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var outroAlunoId = await SeedAlunoAsync(ctx);
        var treino1 = await SeedTreinoAsync(ctx, treinadorId);
        var treino2 = await SeedTreinoAsync(ctx, treinadorId);
        var treino3 = await SeedTreinoAsync(ctx, treinadorId);

        await SeedTreinoAlunoAsync(ctx, treino1.Id, alunoId, TreinoAlunoStatus.Ativo);
        await SeedTreinoAlunoAsync(ctx, treino2.Id, alunoId, TreinoAlunoStatus.Inativo);
        await SeedTreinoAlunoAsync(ctx, treino3.Id, outroAlunoId, TreinoAlunoStatus.Ativo);

        var result = await Repo(ctx).ContarAtivosPorAlunoAsync(alunoId);

        result.Should().Be(1);
    }

    [Fact]
    public async Task ContarAtivosPorAlunoAsync_AlunoSemVinculos_RetornaZero()
    {
        await using var ctx = fixture.CreateContext();
        var alunoId = await SeedAlunoAsync(ctx);

        var result = await Repo(ctx).ContarAtivosPorAlunoAsync(alunoId);

        result.Should().Be(0);
    }

    // --- ListarAtivosPorParAsync ---

    [Fact]
    public async Task ListarAtivosPorParAsync_RetornaAtivosDoParTreinadorAluno()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino1 = await SeedTreinoAsync(ctx, treinadorId);
        var treino2 = await SeedTreinoAsync(ctx, treinadorId);

        var ta1 = await SeedTreinoAlunoAsync(ctx, treino1.Id, alunoId);
        var ta2 = await SeedTreinoAlunoAsync(ctx, treino2.Id, alunoId);

        var result = await Repo(ctx).ListarAtivosPorParAsync(treinadorId, alunoId);

        result.Select(ta => ta.Id).Should().BeEquivalentTo(new[] { ta1.Id, ta2.Id });
    }

    [Fact]
    public async Task ListarAtivosPorParAsync_ExcluiInativos()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        await SeedTreinoAlunoAsync(ctx, treino.Id, alunoId, TreinoAlunoStatus.Inativo);

        var result = await Repo(ctx).ListarAtivosPorParAsync(treinadorId, alunoId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListarAtivosPorParAsync_IsolaOutroTreinador()
    {
        await using var ctx = fixture.CreateContext();
        var treinador1Id = await SeedTreinadorAsync(ctx);
        var treinador2Id = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino2 = await SeedTreinoAsync(ctx, treinador2Id);
        await SeedTreinoAlunoAsync(ctx, treino2.Id, alunoId);

        var result = await Repo(ctx).ListarAtivosPorParAsync(treinador1Id, alunoId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListarAtivosPorParAsync_IsolaOutroAluno()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var outroAlunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        await SeedTreinoAlunoAsync(ctx, treino.Id, outroAlunoId);

        var result = await Repo(ctx).ListarAtivosPorParAsync(treinadorId, alunoId);

        result.Should().BeEmpty();
    }

    // --- ListarAtivosPorAlunoAsync ---

    [Fact]
    public async Task ListarAtivosPorAlunoAsync_RetornaTodosOsAtivosDoAluno()
    {
        await using var ctx = fixture.CreateContext();
        var treinador1Id = await SeedTreinadorAsync(ctx);
        var treinador2Id = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino1 = await SeedTreinoAsync(ctx, treinador1Id);
        var treino2 = await SeedTreinoAsync(ctx, treinador2Id);

        var ta1 = await SeedTreinoAlunoAsync(ctx, treino1.Id, alunoId);
        var ta2 = await SeedTreinoAlunoAsync(ctx, treino2.Id, alunoId);

        var result = await Repo(ctx).ListarAtivosPorAlunoAsync(alunoId);

        result.Select(ta => ta.Id).Should().BeEquivalentTo(new[] { ta1.Id, ta2.Id });
    }

    [Fact]
    public async Task ListarAtivosPorAlunoAsync_ExcluiInativos()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        await SeedTreinoAlunoAsync(ctx, treino.Id, alunoId, TreinoAlunoStatus.Inativo);

        var result = await Repo(ctx).ListarAtivosPorAlunoAsync(alunoId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListarAtivosPorAlunoAsync_IsolaOutrosAlunos()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var outroAlunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        await SeedTreinoAlunoAsync(ctx, treino.Id, outroAlunoId);

        var result = await Repo(ctx).ListarAtivosPorAlunoAsync(alunoId);

        result.Should().BeEmpty();
    }

    // --- ListarAtivosComNomePorParAsync ---

    [Fact]
    public async Task ListarAtivosComNomePorParAsync_ProjetaNomeDoTreino()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        var ta = await SeedTreinoAlunoAsync(ctx, treino.Id, alunoId);

        var result = await Repo(ctx).ListarAtivosComNomePorParAsync(treinadorId, alunoId);

        result.Should().ContainSingle();
        result[0].TreinoAluno.Id.Should().Be(ta.Id);
        result[0].NomeTreino.Should().Be(treino.Nome);
    }

    [Fact]
    public async Task ListarAtivosComNomePorParAsync_ExcluiInativosEOutroTreinador()
    {
        await using var ctx = fixture.CreateContext();
        var treinador1Id = await SeedTreinadorAsync(ctx);
        var treinador2Id = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);

        var treinoInativo = await SeedTreinoAsync(ctx, treinador1Id);
        await SeedTreinoAlunoAsync(ctx, treinoInativo.Id, alunoId, TreinoAlunoStatus.Inativo);

        var treinoOutroTreinador = await SeedTreinoAsync(ctx, treinador2Id);
        await SeedTreinoAlunoAsync(ctx, treinoOutroTreinador.Id, alunoId);

        var result = await Repo(ctx).ListarAtivosComNomePorParAsync(treinador1Id, alunoId);

        result.Should().BeEmpty();
    }

    // --- ListarAtivosComNomePorAlunoAsync ---

    [Fact]
    public async Task ListarAtivosComNomePorAlunoAsync_ProjetaNomeDoTreino()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        var ta = await SeedTreinoAlunoAsync(ctx, treino.Id, alunoId);

        var result = await Repo(ctx).ListarAtivosComNomePorAlunoAsync(alunoId);

        result.Should().ContainSingle();
        result[0].TreinoAluno.Id.Should().Be(ta.Id);
        result[0].NomeTreino.Should().Be(treino.Nome);
    }

    [Fact]
    public async Task ListarAtivosComNomePorAlunoAsync_ExcluiInativosEOutrosAlunos()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var outroAlunoId = await SeedAlunoAsync(ctx);

        var treinoInativo = await SeedTreinoAsync(ctx, treinadorId);
        await SeedTreinoAlunoAsync(ctx, treinoInativo.Id, alunoId, TreinoAlunoStatus.Inativo);

        var treinoOutroAluno = await SeedTreinoAsync(ctx, treinadorId);
        await SeedTreinoAlunoAsync(ctx, treinoOutroAluno.Id, outroAlunoId);

        var result = await Repo(ctx).ListarAtivosComNomePorAlunoAsync(alunoId);

        result.Should().BeEmpty();
    }

    // --- ObterComNomeAsync ---

    [Fact]
    public async Task ObterComNomeAsync_IncluiInativo_FiltraPorAluno()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        var ta = await SeedTreinoAlunoAsync(ctx, treino.Id, alunoId, TreinoAlunoStatus.Inativo);

        var result = await Repo(ctx).ObterComNomeAsync(ta.Id, alunoId);

        result.Should().NotBeNull();
        result!.TreinoAluno.Id.Should().Be(ta.Id);
        result.TreinoAluno.Status.Should().Be(TreinoAlunoStatus.Inativo);
        result.NomeTreino.Should().Be(treino.Nome);
    }

    [Fact]
    public async Task ObterComNomeAsync_OutroAluno_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var outroAlunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        var ta = await SeedTreinoAlunoAsync(ctx, treino.Id, alunoId);

        var result = await Repo(ctx).ObterComNomeAsync(ta.Id, outroAlunoId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ObterComNomeAsync_IdInexistente_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();
        var alunoId = await SeedAlunoAsync(ctx);

        var result = await Repo(ctx).ObterComNomeAsync(Guid.NewGuid(), alunoId);

        result.Should().BeNull();
    }

    // --- ListarDetalhesPorAlunoAsync ---

    [Fact]
    public async Task ListarDetalhesPorAlunoAsync_OrdenaDescPorCreatedAt()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var baseTime = DateTime.UtcNow.AddDays(-10);

        var treino1 = await SeedTreinoAsync(ctx, treinadorId);
        var ta1 = await SeedTreinoAlunoEmAsync(ctx, treino1.Id, alunoId, baseTime);
        var treino2 = await SeedTreinoAsync(ctx, treinadorId);
        var ta2 = await SeedTreinoAlunoEmAsync(ctx, treino2.Id, alunoId, baseTime.AddMinutes(5));

        var (items, total) = await Repo(ctx).ListarDetalhesPorAlunoAsync(alunoId, 1, 50);

        total.Should().Be(2);
        items.Select(i => i.TreinoAluno.Id).Should().Equal(ta2.Id, ta1.Id);
    }

    [Fact]
    public async Task ListarDetalhesPorAlunoAsync_ExcluiInativosEOutroAluno()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var outroAlunoId = await SeedAlunoAsync(ctx);

        var treinoInativo = await SeedTreinoAsync(ctx, treinadorId);
        await SeedTreinoAlunoAsync(ctx, treinoInativo.Id, alunoId, TreinoAlunoStatus.Inativo);

        var treinoOutroAluno = await SeedTreinoAsync(ctx, treinadorId);
        await SeedTreinoAlunoAsync(ctx, treinoOutroAluno.Id, outroAlunoId);

        var (items, total) = await Repo(ctx).ListarDetalhesPorAlunoAsync(alunoId, 1, 50);

        items.Should().BeEmpty();
        total.Should().Be(0);
    }

    [Fact]
    public async Task ListarDetalhesPorAlunoAsync_Paginacao_RetornaSubset()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var baseTime = DateTime.UtcNow.AddDays(-10);

        for (var i = 0; i < 5; i++)
        {
            var treino = await SeedTreinoAsync(ctx, treinadorId);
            await SeedTreinoAlunoEmAsync(ctx, treino.Id, alunoId, baseTime.AddMinutes(i));
        }

        var (page, total) = await Repo(ctx).ListarDetalhesPorAlunoAsync(alunoId, 2, 2);

        page.Should().HaveCount(2);
        total.Should().Be(5);
    }

    [Fact]
    public async Task ListarDetalhesPorAlunoAsync_CarregaExerciciosESeries()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoComExercicioESerieAsync(ctx, treinadorId);
        await SeedTreinoAlunoAsync(ctx, treino.Id, alunoId);

        var (items, _) = await Repo(ctx).ListarDetalhesPorAlunoAsync(alunoId, 1, 50);

        items.Should().ContainSingle();
        items[0].Treino.Exercicios.Should().ContainSingle();
        items[0].Treino.Exercicios[0].Series.Should().ContainSingle();
    }

    // --- ObterDetalheAsync ---

    [Fact]
    public async Task ObterDetalheAsync_CarregaExerciciosESeries()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoComExercicioESerieAsync(ctx, treinadorId);
        var ta = await SeedTreinoAlunoAsync(ctx, treino.Id, alunoId);

        var result = await Repo(ctx).ObterDetalheAsync(ta.Id, alunoId);

        result.Should().NotBeNull();
        result!.TreinoAluno.Id.Should().Be(ta.Id);
        result.Treino.Exercicios.Should().ContainSingle();
        result.Treino.Exercicios[0].Series.Should().ContainSingle();
    }

    [Fact]
    public async Task ObterDetalheAsync_OutroAluno_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var outroAlunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        var ta = await SeedTreinoAlunoAsync(ctx, treino.Id, alunoId);

        var result = await Repo(ctx).ObterDetalheAsync(ta.Id, outroAlunoId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ObterDetalheAsync_NaoFiltraPorStatus_RetornaInativo()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        var ta = await SeedTreinoAlunoAsync(ctx, treino.Id, alunoId, TreinoAlunoStatus.Inativo);

        var result = await Repo(ctx).ObterDetalheAsync(ta.Id, alunoId);

        result.Should().NotBeNull();
        result!.TreinoAluno.Id.Should().Be(ta.Id);
    }

    // --- ObterDetalheAdminAsync ---

    [Fact]
    public async Task ObterDetalheAdminAsync_IgnoraAluno_RetornaDetalhe()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoComExercicioESerieAsync(ctx, treinadorId);
        var ta = await SeedTreinoAlunoAsync(ctx, treino.Id, alunoId);

        var result = await Repo(ctx).ObterDetalheAdminAsync(ta.Id);

        result.Should().NotBeNull();
        result!.TreinoAluno.Id.Should().Be(ta.Id);
        result.Treino.Exercicios.Should().ContainSingle();
        result.Treino.Exercicios[0].Series.Should().ContainSingle();
    }

    [Fact]
    public async Task ObterDetalheAdminAsync_IdInexistente_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();

        var result = await Repo(ctx).ObterDetalheAdminAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    // --- AdicionarAsync ---

    [Fact]
    public async Task AdicionarAsync_ComSaveChanges_Persiste()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        var ta = TreinoAluno.Criar(treino.Id, alunoId, DateTime.UtcNow).Value;

        await Repo(ctx).AdicionarAsync(ta);
        await ctx.SaveChangesAsync();

        await using var ctxVerifica = fixture.CreateContext();
        var persisted = await ctxVerifica.TreinoAlunos.FindAsync(ta.Id);
        persisted.Should().NotBeNull();
        persisted!.AlunoId.Should().Be(alunoId);
    }

    // --- RemoverPorTreinoIdAsync ---

    [Fact]
    public async Task RemoverPorTreinoIdAsync_ComSaveChanges_RemoveVinculosDoTreino()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var aluno1Id = await SeedAlunoAsync(ctx);
        var aluno2Id = await SeedAlunoAsync(ctx);
        var treinoAlvo = await SeedTreinoAsync(ctx, treinadorId);
        var treinoOutro = await SeedTreinoAsync(ctx, treinadorId);

        var taAlvo1 = await SeedTreinoAlunoAsync(ctx, treinoAlvo.Id, aluno1Id);
        var taAlvo2 = await SeedTreinoAlunoAsync(ctx, treinoAlvo.Id, aluno2Id);
        var taOutro = await SeedTreinoAlunoAsync(ctx, treinoOutro.Id, aluno1Id);

        await Repo(ctx).RemoverPorTreinoIdAsync(treinoAlvo.Id);
        await ctx.SaveChangesAsync();

        await using var ctxVerifica = fixture.CreateContext();
        var restantes = await ctxVerifica.TreinoAlunos
            .Where(ta => ta.Id == taAlvo1.Id || ta.Id == taAlvo2.Id || ta.Id == taOutro.Id)
            .ToListAsync();

        restantes.Should().ContainSingle();
        restantes[0].Id.Should().Be(taOutro.Id);
    }

    // --- ListarAtivosPorTreinoIdAsync ---

    [Fact]
    public async Task ListarAtivosPorTreinoIdAsync_ProjetaNomeDoAluno()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var email = Email.Criar($"a{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        var aluno = Aluno.Criar(conta.Id, "Aluno Nomeado", DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.SaveChangesAsync();
        var treino = await SeedTreinoAsync(ctx, treinadorId);
        var ta = await SeedTreinoAlunoAsync(ctx, treino.Id, aluno.Id);

        var result = await Repo(ctx).ListarAtivosPorTreinoIdAsync(treino.Id);

        result.Should().ContainSingle();
        result[0].TreinoAlunoId.Should().Be(ta.Id);
        result[0].AlunoId.Should().Be(aluno.Id);
        result[0].NomeAluno.Should().Be("Aluno Nomeado");
        result[0].Status.Should().Be(TreinoAlunoStatus.Ativo);
    }

    [Fact]
    public async Task ListarAtivosPorTreinoIdAsync_ExcluiInativosEOutroTreino()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var treinoAlvo = await SeedTreinoAsync(ctx, treinadorId);
        var treinoOutro = await SeedTreinoAsync(ctx, treinadorId);

        await SeedTreinoAlunoAsync(ctx, treinoAlvo.Id, alunoId, TreinoAlunoStatus.Inativo);
        await SeedTreinoAlunoAsync(ctx, treinoOutro.Id, alunoId, TreinoAlunoStatus.Ativo);

        var result = await Repo(ctx).ListarAtivosPorTreinoIdAsync(treinoAlvo.Id);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListarAtivosPorTreinoIdAsync_TreinoSemVinculos_RetornaVazio()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, treinadorId);

        var result = await Repo(ctx).ListarAtivosPorTreinoIdAsync(treino.Id);

        result.Should().BeEmpty();
    }

    // --- ExcluirPorAlunoIdAsync ---

    [Fact]
    public async Task ExcluirPorAlunoIdAsync_ExecutaDeleteImediato_SemSaveChanges()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var outroAlunoId = await SeedAlunoAsync(ctx);
        var treino1 = await SeedTreinoAsync(ctx, treinadorId);
        var treino2 = await SeedTreinoAsync(ctx, treinadorId);

        await SeedTreinoAlunoAsync(ctx, treino1.Id, alunoId);
        var taOutro = await SeedTreinoAlunoAsync(ctx, treino2.Id, outroAlunoId);

        await Repo(ctx).ExcluirPorAlunoIdAsync(alunoId);

        await using var ctxVerifica = fixture.CreateContext();
        var restantes = await ctxVerifica.TreinoAlunos
            .Where(ta => ta.AlunoId == alunoId || ta.AlunoId == outroAlunoId)
            .ToListAsync();

        restantes.Should().ContainSingle();
        restantes[0].Id.Should().Be(taOutro.Id);
    }
}
