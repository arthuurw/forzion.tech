using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class TreinoRepositoryTests(InfrastructureTestFixture fixture)
{
    private static TreinoRepository Repo(AppDbContext ctx) => new(ctx);

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

    private static async Task<Aluno> SeedAlunoAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"a{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        var aluno = Aluno.Criar(conta.Id, "Aluno", DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.SaveChangesAsync();
        return aluno;
    }

    private static async Task<Treino> SeedTreinoAsync(AppDbContext ctx, Guid treinadorId,
        string nome, ObjetivoTreino objetivo = ObjetivoTreino.Hipertrofia, DateTime? createdAt = null,
        DificuldadeTreino dificuldade = DificuldadeTreino.Iniciante)
    {
        var treino = Treino.Criar(nome, objetivo, treinadorId, createdAt ?? DateTime.UtcNow, dificuldade).Value;
        await ctx.Treinos.AddAsync(treino);
        await ctx.SaveChangesAsync();
        return treino;
    }

    private static async Task<Guid> SeedExercicioAsync(AppDbContext ctx)
    {
        var grupo = GrupoMuscular.Criar($"G-{Guid.NewGuid():N}", DateTime.UtcNow).Value;
        await ctx.GruposMusculares.AddAsync(grupo);
        var ex = Exercicio.Criar($"E-{Guid.NewGuid():N}", grupo.Id, DateTime.UtcNow).Value;
        await ctx.Exercicios.AddAsync(ex);
        await ctx.SaveChangesAsync();
        return ex.Id;
    }

    // --- ListarPorTreinadorAsync ---

    [Fact]
    public async Task ListarPorTreinadorAsync_SemFiltro_RetornaTreinos()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        await SeedTreinoAsync(ctx, tid, $"Treino-{Guid.NewGuid():N}");

        var (items, total) = await Repo(ctx).ListarPorTreinadorAsync(tid, 1, 50);

        items.Should().NotBeEmpty();
        total.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_IsolaOutrosTreinadores()
    {
        await using var ctx = fixture.CreateContext();
        var tid1 = await SeedTreinadorAsync(ctx);
        var tid2 = await SeedTreinadorAsync(ctx);
        await SeedTreinoAsync(ctx, tid1, $"TreinoT1-{Guid.NewGuid():N}");
        await SeedTreinoAsync(ctx, tid2, $"TreinoT2-{Guid.NewGuid():N}");

        var (items, _) = await Repo(ctx).ListarPorTreinadorAsync(tid1, 1, 50);

        items.Should().AllSatisfy(i => i.Treino.TreinadorId.Should().Be(tid1));
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_FiltroNome_RetornaApenasMatch()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        await SeedTreinoAsync(ctx, tid, $"Costas-{Guid.NewGuid():N}");
        await SeedTreinoAsync(ctx, tid, $"Peito-{Guid.NewGuid():N}");

        var (items, _) = await Repo(ctx).ListarPorTreinadorAsync(tid, 1, 50, nome: "Costas");

        items.Should().AllSatisfy(i => i.Treino.Nome.Should().Contain("Costas"));
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_FiltroObjetivo_RetornaApenasObjetivo()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        await SeedTreinoAsync(ctx, tid, $"Hip-{Guid.NewGuid():N}", ObjetivoTreino.Hipertrofia);
        await SeedTreinoAsync(ctx, tid, $"Forca-{Guid.NewGuid():N}", ObjetivoTreino.Forca);

        var (items, _) = await Repo(ctx).ListarPorTreinadorAsync(tid, 1, 50, objetivo: "Hipertrofia");

        items.Should().AllSatisfy(i => i.Treino.Objetivo.Should().Be(ObjetivoTreino.Hipertrofia));
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_OrdenarPorObjetivo_RetornaOrdenado()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        await SeedTreinoAsync(ctx, tid, $"Emagrecimento-{Guid.NewGuid():N}", ObjetivoTreino.Emagrecimento);
        await SeedTreinoAsync(ctx, tid, $"Forca-{Guid.NewGuid():N}", ObjetivoTreino.Forca);
        await SeedTreinoAsync(ctx, tid, $"Hip-{Guid.NewGuid():N}", ObjetivoTreino.Hipertrofia);

        var (items, _) = await Repo(ctx).ListarPorTreinadorAsync(tid, 1, 50, ordenarPor: "objetivo");

        items.Select(i => i.Treino.Objetivo.ToString()).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_OrdenarPorCreatedAt_RetornaOrdenadoDesc()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        // Timestamps explícitos eliminam `Task.Delay(10)` (flaky em CI lento) — a
        // ordenação por CreatedAt é determinística desde o seed.
        var baseTime = DateTime.UtcNow;
        var t1 = await SeedTreinoAsync(ctx, tid, $"Antigo-{Guid.NewGuid():N}", createdAt: baseTime);
        await SeedTreinoAsync(ctx, tid, $"Novo-{Guid.NewGuid():N}", createdAt: baseTime.AddSeconds(1));

        var (items, _) = await Repo(ctx).ListarPorTreinadorAsync(tid, 1, 50, ordenarPor: "createdAt");

        items.First().Treino.Id.Should().NotBe(t1.Id);
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_OrdenarPorDificuldade_RetornaOrdenado()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        await SeedTreinoAsync(ctx, tid, $"Av-{Guid.NewGuid():N}", dificuldade: DificuldadeTreino.Avancado);
        await SeedTreinoAsync(ctx, tid, $"Ini-{Guid.NewGuid():N}", dificuldade: DificuldadeTreino.Iniciante);
        await SeedTreinoAsync(ctx, tid, $"Int-{Guid.NewGuid():N}", dificuldade: DificuldadeTreino.Intermediario);

        var (items, _) = await Repo(ctx).ListarPorTreinadorAsync(tid, 1, 50, ordenarPor: "dificuldade");

        items.Select(i => (int)i.Treino.Dificuldade).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_OrdenarPorExercicios_RetornaContagemDesc()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        var ex1 = await SeedExercicioAsync(ctx);
        var ex2 = await SeedExercicioAsync(ctx);
        await SeedTreinoAsync(ctx, tid, $"Vazio-{Guid.NewGuid():N}");
        var cheio = await SeedTreinoAsync(ctx, tid, $"Cheio-{Guid.NewGuid():N}");
        // Adiciona o filho explícito ao contexto como o handler faz (AdicionarTreinoExercicioAsync):
        // PK Guid é ValueGeneratedOnAdd, então um filho novo descoberto só via navegação de
        // agregado já rastreado vira UPDATE (0 linhas), não INSERT.
        await ctx.TreinoExercicios.AddAsync(cheio.AdicionarExercicio(ex1, DateTime.UtcNow).Value);
        await ctx.TreinoExercicios.AddAsync(cheio.AdicionarExercicio(ex2, DateTime.UtcNow).Value);
        await ctx.SaveChangesAsync();

        var (items, _) = await Repo(ctx).ListarPorTreinadorAsync(tid, 1, 50, ordenarPor: "exercicios");

        items.Select(i => i.Treino.Exercicios.Count).Should().BeInDescendingOrder();
        items.First().Treino.Id.Should().Be(cheio.Id);
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_TreinoSemAluno_NomeAlunoNulo()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, tid, $"SemAluno-{Guid.NewGuid():N}");

        var (items, _) = await Repo(ctx).ListarPorTreinadorAsync(tid, 1, 50);

        var item = items.First(i => i.Treino.Id == treino.Id);
        item.NomeAluno.Should().BeNull();
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_TreinoComAluno_NomeAlunoPreenchido()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, tid, $"ComAluno-{Guid.NewGuid():N}");
        var aluno = await SeedAlunoAsync(ctx);

        var treinoAluno = TreinoAluno.Criar(treino.Id, aluno.Id, DateTime.UtcNow).Value;
        await ctx.TreinoAlunos.AddAsync(treinoAluno);
        await ctx.SaveChangesAsync();

        var (items, _) = await Repo(ctx).ListarPorTreinadorAsync(tid, 1, 50, ordenarPor: "nomeAluno");

        var item = items.First(i => i.Treino.Id == treino.Id);
        item.NomeAluno.Should().Be(aluno.Nome);
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_Paginacao_RetornaSubset()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        for (var i = 0; i < 5; i++)
            await SeedTreinoAsync(ctx, tid, $"PagTreino{i}-{Guid.NewGuid():N}");

        var (page, total) = await Repo(ctx).ListarPorTreinadorAsync(tid, 1, 3);

        page.Should().HaveCount(3);
        total.Should().BeGreaterThanOrEqualTo(5);
    }

    // --- ListarPorAlunoAsync ---

    [Fact]
    public async Task ListarPorAlunoAsync_AlunoComFichasAtivas_RetornaFichas()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        var aluno = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, tid, $"FichaAluno-{Guid.NewGuid():N}");

        var treinoAluno = TreinoAluno.Criar(treino.Id, aluno.Id, DateTime.UtcNow).Value;
        await ctx.TreinoAlunos.AddAsync(treinoAluno);
        await ctx.SaveChangesAsync();

        var (items, total) = await Repo(ctx).ListarPorAlunoAsync(aluno.Id, 1, 50);

        items.Should().ContainSingle(t => t.Id == treino.Id);
        total.Should().Be(1);
    }

    [Fact]
    public async Task ListarPorAlunoAsync_AlunoSemFichas_RetornaVazio()
    {
        await using var ctx = fixture.CreateContext();
        var aluno = await SeedAlunoAsync(ctx);

        var (items, total) = await Repo(ctx).ListarPorAlunoAsync(aluno.Id, 1, 50);

        items.Should().BeEmpty();
        total.Should().Be(0);
    }

    [Fact]
    public async Task ListarPorAlunoAsync_IsolaOutrosAlunos()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        var aluno1 = await SeedAlunoAsync(ctx);
        var aluno2 = await SeedAlunoAsync(ctx);
        var treino = await SeedTreinoAsync(ctx, tid, $"FichaIsolada-{Guid.NewGuid():N}");

        var treinoAluno = TreinoAluno.Criar(treino.Id, aluno1.Id, DateTime.UtcNow).Value;
        await ctx.TreinoAlunos.AddAsync(treinoAluno);
        await ctx.SaveChangesAsync();

        var (items, _) = await Repo(ctx).ListarPorAlunoAsync(aluno2.Id, 1, 50);

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListarPorAlunoAsync_Paginacao_RetornaSubset()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        var aluno = await SeedAlunoAsync(ctx);

        for (var i = 0; i < 5; i++)
        {
            var treino = await SeedTreinoAsync(ctx, tid, $"FichaPag{i}-{Guid.NewGuid():N}");
            var ta = TreinoAluno.Criar(treino.Id, aluno.Id, DateTime.UtcNow).Value;
            await ctx.TreinoAlunos.AddAsync(ta);
        }
        await ctx.SaveChangesAsync();

        var (page, total) = await Repo(ctx).ListarPorAlunoAsync(aluno.Id, 1, 3);

        page.Should().HaveCount(3);
        total.Should().BeGreaterThanOrEqualTo(5);
    }
}
