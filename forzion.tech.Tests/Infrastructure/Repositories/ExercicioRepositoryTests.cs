using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
public class ExercicioRepositoryTests(InfrastructureTestFixture fixture)
{
    private static ExercicioRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<Guid> SeedTreinadorIdAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"t{Guid.NewGuid():N}@test.com");
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador);
        var treinador = Treinador.Criar(conta.Id, "Treinador");
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        return treinador.Id;
    }

    private static async Task<Exercicio> SeedAsync(
        AppDbContext ctx, string nome, TipoGrupoMuscular grupo, Guid? treinadorId = null)
    {
        var ex = Exercicio.Criar(nome, grupo, treinadorId);
        await ctx.Exercicios.AddAsync(ex);
        await ctx.SaveChangesAsync();
        return ex;
    }

    // --- ObterNomesPorIdsAsync ---

    [Fact]
    public async Task ObterNomesPorIdsAsync_ListaVazia_RetornaDicionarioVazio()
    {
        await using var ctx = fixture.CreateContext();

        var result = await Repo(ctx).ObterNomesPorIdsAsync([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ObterNomesPorIdsAsync_IdExistente_RetornaNome()
    {
        await using var ctx = fixture.CreateContext();
        var ex = await SeedAsync(ctx, $"Supino-{Guid.NewGuid():N}", TipoGrupoMuscular.Peito);

        var result = await Repo(ctx).ObterNomesPorIdsAsync([ex.Id]);

        result.Should().ContainKey(ex.Id);
        result[ex.Id].Should().Be(ex.Nome);
    }

    [Fact]
    public async Task ObterNomesPorIdsAsync_MultiploIds_RetornaTodos()
    {
        await using var ctx = fixture.CreateContext();
        var ex1 = await SeedAsync(ctx, $"Rosca-{Guid.NewGuid():N}", TipoGrupoMuscular.Biceps);
        var ex2 = await SeedAsync(ctx, $"Remada-{Guid.NewGuid():N}", TipoGrupoMuscular.Costas);

        var result = await Repo(ctx).ObterNomesPorIdsAsync([ex1.Id, ex2.Id]);

        result.Should().ContainKey(ex1.Id).And.ContainKey(ex2.Id);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ObterNomesPorIdsAsync_IdNaoExistente_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();

        var result = await Repo(ctx).ObterNomesPorIdsAsync([Guid.NewGuid()]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ObterNomesPorIdsAsync_IdsDuplicados_SemDuplicataNoDicionario()
    {
        await using var ctx = fixture.CreateContext();
        var ex = await SeedAsync(ctx, $"Dup-{Guid.NewGuid():N}", TipoGrupoMuscular.Core);

        var result = await Repo(ctx).ObterNomesPorIdsAsync([ex.Id, ex.Id]);

        result.Should().HaveCount(1);
    }

    // --- ListarAsync ---

    [Fact]
    public async Task ListarAsync_FiltroNome_RetornaApenasMatch()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        await SeedAsync(ctx, $"Rosca-{Guid.NewGuid():N}", TipoGrupoMuscular.Biceps, tid);
        await SeedAsync(ctx, $"Supino-{Guid.NewGuid():N}", TipoGrupoMuscular.Peito, tid);

        var (items, total) = await Repo(ctx).ListarAsync(tid, 1, 50, nome: "Rosca");

        items.Should().AllSatisfy(e => e.Nome.Should().Contain("Rosca"));
        total.Should().Be(items.Count);
    }

    [Fact]
    public async Task ListarAsync_FiltroGrupoMuscular_RetornaApenasGrupo()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        await SeedAsync(ctx, $"Rosca-{Guid.NewGuid():N}", TipoGrupoMuscular.Biceps, tid);
        await SeedAsync(ctx, $"Agachamento-{Guid.NewGuid():N}", TipoGrupoMuscular.Pernas, tid);

        var (items, _) = await Repo(ctx).ListarAsync(tid, 1, 50, grupoMuscular: TipoGrupoMuscular.Biceps);

        items.Should().AllSatisfy(e => e.GrupoMuscular.Should().Be(TipoGrupoMuscular.Biceps));
    }

    [Fact]
    public async Task ListarAsync_OrdenarPorGrupoMuscular_RetornaOrdenadoPorGrupo()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        await SeedAsync(ctx, $"Peito-{Guid.NewGuid():N}", TipoGrupoMuscular.Peito, tid);
        await SeedAsync(ctx, $"Biceps-{Guid.NewGuid():N}", TipoGrupoMuscular.Biceps, tid);
        await SeedAsync(ctx, $"Pernas-{Guid.NewGuid():N}", TipoGrupoMuscular.Pernas, tid);

        var (items, _) = await Repo(ctx).ListarAsync(tid, 1, 50, ordenarPor: "grupoMuscular");

        items.Select(e => e.GrupoMuscular.ToString()).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ListarAsync_OrdenacaoPadrao_RetornaOrdenadoPorNome()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        await SeedAsync(ctx, $"Zumba-{Guid.NewGuid():N}", TipoGrupoMuscular.Core, tid);
        await SeedAsync(ctx, $"Abdominal-{Guid.NewGuid():N}", TipoGrupoMuscular.Core, tid);

        var (items, _) = await Repo(ctx).ListarAsync(tid, 1, 50);

        items.Should().BeInAscendingOrder(e => e.Nome);
    }

    [Fact]
    public async Task ListarAsync_IsolaExerciciosPorTreinador()
    {
        await using var ctx = fixture.CreateContext();
        var tid1 = await SeedTreinadorIdAsync(ctx);
        var tid2 = await SeedTreinadorIdAsync(ctx);
        await SeedAsync(ctx, $"ExT1-{Guid.NewGuid():N}", TipoGrupoMuscular.Peito, tid1);
        await SeedAsync(ctx, $"ExT2-{Guid.NewGuid():N}", TipoGrupoMuscular.Costas, tid2);

        var (items, _) = await Repo(ctx).ListarAsync(tid1, 1, 50);

        items.Should().AllSatisfy(e => e.TreinadorId.Should().Be(tid1));
    }

    [Fact]
    public async Task ListarAsync_Paginacao_RetornaSubset()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        for (var i = 0; i < 5; i++)
            await SeedAsync(ctx, $"Pag{i}-{Guid.NewGuid():N}", TipoGrupoMuscular.Core, tid);

        var (page, total) = await Repo(ctx).ListarAsync(tid, 1, 3);

        page.Should().HaveCount(3);
        total.Should().BeGreaterThanOrEqualTo(5);
    }

    // --- NomeJaExisteAsync ---

    [Fact]
    public async Task NomeJaExisteAsync_NomeIgual_RetornaTrue()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        await SeedAsync(ctx, "Push Up", TipoGrupoMuscular.Peito, tid);

        var result = await Repo(ctx).NomeJaExisteAsync("Push Up", tid);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task NomeJaExisteAsync_CaseInsensitive_RetornaTrue()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        await SeedAsync(ctx, "Pull Up", TipoGrupoMuscular.Costas, tid);

        var result = await Repo(ctx).NomeJaExisteAsync("pull up", tid);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task NomeJaExisteAsync_ComExcludeId_IgnoraProprio()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        var ex = await SeedAsync(ctx, "Leg Press", TipoGrupoMuscular.Pernas, tid);

        var result = await Repo(ctx).NomeJaExisteAsync("Leg Press", tid, excludeId: ex.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task NomeJaExisteAsync_OutroTreinador_RetornaFalse()
    {
        await using var ctx = fixture.CreateContext();
        var tid1 = await SeedTreinadorIdAsync(ctx);
        var tid2 = await SeedTreinadorIdAsync(ctx);
        await SeedAsync(ctx, "Flexao", TipoGrupoMuscular.Peito, tid1);

        var result = await Repo(ctx).NomeJaExisteAsync("Flexao", tid2);

        result.Should().BeFalse();
    }

    // --- EstaEmUsoAsync ---

    [Fact]
    public async Task EstaEmUsoAsync_ExercicioEmTreino_RetornaTrue()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        var ex = await SeedAsync(ctx, $"Usado-{Guid.NewGuid():N}", TipoGrupoMuscular.Peito, tid);

        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, tid);
        treino.AdicionarExercicio(ex.Id);
        await ctx.Treinos.AddAsync(treino);
        await ctx.SaveChangesAsync();

        var result = await Repo(ctx).EstaEmUsoAsync(ex.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EstaEmUsoAsync_ExercicioSemTreino_RetornaFalse()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        var ex = await SeedAsync(ctx, $"Livre-{Guid.NewGuid():N}", TipoGrupoMuscular.Core, tid);

        var result = await Repo(ctx).EstaEmUsoAsync(ex.Id);

        result.Should().BeFalse();
    }

    // --- ExisteAsync ---

    [Fact]
    public async Task ExisteAsync_ExercicioGlobal_QualquerTreinadorRetornaTrue()
    {
        await using var ctx = fixture.CreateContext();
        var ex = await SeedAsync(ctx, $"Global-{Guid.NewGuid():N}", TipoGrupoMuscular.FullBody);

        var result = await Repo(ctx).ExisteAsync(ex.Id, Guid.NewGuid());

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExisteAsync_ExercicioDoTreinador_TreinadorCorretoRetornaTrue()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        var ex = await SeedAsync(ctx, $"Proprio-{Guid.NewGuid():N}", TipoGrupoMuscular.Ombro, tid);

        var result = await Repo(ctx).ExisteAsync(ex.Id, tid);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExisteAsync_ExercicioDeOutroTreinador_RetornaFalse()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        var ex = await SeedAsync(ctx, $"Alheio-{Guid.NewGuid():N}", TipoGrupoMuscular.Triceps, tid);

        var result = await Repo(ctx).ExisteAsync(ex.Id, Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExisteAsync_IdInexistente_RetornaFalse()
    {
        await using var ctx = fixture.CreateContext();

        var result = await Repo(ctx).ExisteAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeFalse();
    }
}
