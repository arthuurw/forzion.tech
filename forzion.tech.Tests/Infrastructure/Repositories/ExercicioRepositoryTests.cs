using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
public class ExercicioRepositoryTests(InfrastructureTestFixture fixture)
{
    private static ExercicioRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<Guid> SeedTreinadorIdAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"t{Guid.NewGuid():N}@test.com");
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, DateTime.UtcNow);
        var treinador = Treinador.Criar(conta.Id, "Treinador", DateTime.UtcNow);
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        return treinador.Id;
    }

    private static async Task<Guid> SeedGrupoAsync(AppDbContext ctx, string? nome = null)
    {
        var grupo = GrupoMuscular.Criar(nome ?? $"G-{Guid.NewGuid():N}", DateTime.UtcNow);
        await ctx.GruposMusculares.AddAsync(grupo);
        await ctx.SaveChangesAsync();
        return grupo.Id;
    }

    private static async Task<Exercicio> SeedAsync(
        AppDbContext ctx, string nome, Guid grupoMuscularId, Guid? treinadorId = null)
    {
        var ex = Exercicio.Criar(nome, grupoMuscularId, DateTime.UtcNow, treinadorId);
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
        var ex = await SeedAsync(ctx, $"Supino-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx));

        var result = await Repo(ctx).ObterNomesPorIdsAsync([ex.Id]);

        result.Should().ContainKey(ex.Id);
        result[ex.Id].Should().Be(ex.Nome);
    }

    [Fact]
    public async Task ObterNomesPorIdsAsync_MultiploIds_RetornaTodos()
    {
        await using var ctx = fixture.CreateContext();
        var ex1 = await SeedAsync(ctx, $"Rosca-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx));
        var ex2 = await SeedAsync(ctx, $"Remada-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx));

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
        var ex = await SeedAsync(ctx, $"Dup-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx));

        var result = await Repo(ctx).ObterNomesPorIdsAsync([ex.Id, ex.Id]);

        result.Should().HaveCount(1);
    }

    // --- ListarAsync ---

    [Fact]
    public async Task ListarAsync_FiltroNome_RetornaApenasMatch()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        await SeedAsync(ctx, $"Rosca-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx), tid);
        await SeedAsync(ctx, $"Supino-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx), tid);

        var (items, total) = await Repo(ctx).ListarAsync(tid, 1, 50, nome: "Rosca");

        items.Should().AllSatisfy(e => e.Nome.Should().Contain("Rosca"));
        total.Should().Be(items.Count);
    }

    [Fact]
    public async Task ListarAsync_FiltroGrupoMuscular_RetornaApenasGrupo()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        var grupoBiceps = await SeedGrupoAsync(ctx);
        var grupoPernas = await SeedGrupoAsync(ctx);
        await SeedAsync(ctx, $"Rosca-{Guid.NewGuid():N}", grupoBiceps, tid);
        await SeedAsync(ctx, $"Agachamento-{Guid.NewGuid():N}", grupoPernas, tid);

        var (items, _) = await Repo(ctx).ListarAsync(tid, 1, 50, grupoMuscularId: grupoBiceps);

        items.Should().AllSatisfy(e => e.GrupoMuscularId.Should().Be(grupoBiceps));
    }

    [Fact]
    public async Task ListarAsync_OrdenarPorGrupoMuscular_RetornaOrdenadoPorGrupo()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        var gC = await SeedGrupoAsync(ctx, $"C-{Guid.NewGuid():N}");
        var gA = await SeedGrupoAsync(ctx, $"A-{Guid.NewGuid():N}");
        var gB = await SeedGrupoAsync(ctx, $"B-{Guid.NewGuid():N}");
        await SeedAsync(ctx, $"Ex1-{Guid.NewGuid():N}", gC, tid);
        await SeedAsync(ctx, $"Ex2-{Guid.NewGuid():N}", gA, tid);
        await SeedAsync(ctx, $"Ex3-{Guid.NewGuid():N}", gB, tid);

        var (items, _) = await Repo(ctx).ListarAsync(tid, 1, 50, ordenarPor: "grupoMuscular");

        var nomePorId = await ctx.GruposMusculares.ToDictionaryAsync(g => g.Id, g => g.Nome);
        items.Select(e => nomePorId[e.GrupoMuscularId]).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ListarAsync_OrdenacaoPadrao_RetornaOrdenadoPorNome()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        await SeedAsync(ctx, $"Zumba-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx), tid);
        await SeedAsync(ctx, $"Abdominal-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx), tid);

        var (items, _) = await Repo(ctx).ListarAsync(tid, 1, 50);

        items.Should().BeInAscendingOrder(e => e.Nome);
    }

    [Fact]
    public async Task ListarAsync_IsolaExerciciosPorTreinador()
    {
        await using var ctx = fixture.CreateContext();
        var tid1 = await SeedTreinadorIdAsync(ctx);
        var tid2 = await SeedTreinadorIdAsync(ctx);
        await SeedAsync(ctx, $"ExT1-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx), tid1);
        await SeedAsync(ctx, $"ExT2-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx), tid2);

        var (items, _) = await Repo(ctx).ListarAsync(tid1, 1, 50);

        items.Should().AllSatisfy(e => e.TreinadorId.Should().Be(tid1));
    }

    [Fact]
    public async Task ListarAsync_Paginacao_RetornaSubset()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        for (var i = 0; i < 5; i++)
            await SeedAsync(ctx, $"Pag{i}-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx), tid);

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
        await SeedAsync(ctx, "Push Up", await SeedGrupoAsync(ctx), tid);

        var result = await Repo(ctx).NomeJaExisteAsync("Push Up", tid);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task NomeJaExisteAsync_CaseInsensitive_RetornaTrue()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        await SeedAsync(ctx, "Pull Up", await SeedGrupoAsync(ctx), tid);

        var result = await Repo(ctx).NomeJaExisteAsync("pull up", tid);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task NomeJaExisteAsync_ComExcludeId_IgnoraProprio()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        var ex = await SeedAsync(ctx, "Leg Press", await SeedGrupoAsync(ctx), tid);

        var result = await Repo(ctx).NomeJaExisteAsync("Leg Press", tid, excludeId: ex.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task NomeJaExisteAsync_OutroTreinador_RetornaFalse()
    {
        await using var ctx = fixture.CreateContext();
        var tid1 = await SeedTreinadorIdAsync(ctx);
        var tid2 = await SeedTreinadorIdAsync(ctx);
        await SeedAsync(ctx, "Flexao", await SeedGrupoAsync(ctx), tid1);

        var result = await Repo(ctx).NomeJaExisteAsync("Flexao", tid2);

        result.Should().BeFalse();
    }

    // --- EstaEmUsoAsync ---

    [Fact]
    public async Task EstaEmUsoAsync_ExercicioEmTreino_RetornaTrue()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        var ex = await SeedAsync(ctx, $"Usado-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx), tid);

        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, tid, DateTime.UtcNow);
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
        var ex = await SeedAsync(ctx, $"Livre-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx), tid);

        var result = await Repo(ctx).EstaEmUsoAsync(ex.Id);

        result.Should().BeFalse();
    }

    // --- ExisteAsync ---

    [Fact]
    public async Task ExisteAsync_ExercicioGlobal_QualquerTreinadorRetornaTrue()
    {
        await using var ctx = fixture.CreateContext();
        var ex = await SeedAsync(ctx, $"Global-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx));

        var result = await Repo(ctx).ExisteAsync(ex.Id, Guid.NewGuid());

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExisteAsync_ExercicioDoTreinador_TreinadorCorretoRetornaTrue()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        var ex = await SeedAsync(ctx, $"Proprio-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx), tid);

        var result = await Repo(ctx).ExisteAsync(ex.Id, tid);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExisteAsync_ExercicioDeOutroTreinador_RetornaFalse()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorIdAsync(ctx);
        var ex = await SeedAsync(ctx, $"Alheio-{Guid.NewGuid():N}", await SeedGrupoAsync(ctx), tid);

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
