using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class GrupoMuscularRepositoryTests(InfrastructureTestFixture fixture)
{
    private static GrupoMuscularRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<GrupoMuscular> SeedAsync(AppDbContext ctx, string nome)
    {
        var grupo = GrupoMuscular.Criar(nome, DateTime.UtcNow).Value;
        await ctx.Set<GrupoMuscular>().AddAsync(grupo);
        await ctx.SaveChangesAsync();
        return grupo;
    }

    [Fact]
    public async Task ObterPorNomeAsync_EncontraIndependenteDeCaixa()
    {
        await using var ctx = fixture.CreateContext();
        var nome = $"Peito-{Guid.NewGuid():N}";
        await SeedAsync(ctx, nome);

        await using var verifyCtx = fixture.CreateContext();
        var porMinusculo = await Repo(verifyCtx).ObterPorNomeAsync(nome.ToLowerInvariant());
        var porMaiusculo = await Repo(verifyCtx).ObterPorNomeAsync(nome.ToUpperInvariant());

        porMinusculo.Should().NotBeNull();
        porMinusculo!.Nome.Should().Be(nome);
        porMaiusculo.Should().NotBeNull();
        porMaiusculo!.Nome.Should().Be(nome);
    }

    [Fact]
    public async Task ObterPorNomeAsync_NaoFazMatchParcial()
    {
        await using var ctx = fixture.CreateContext();
        var nome = $"Peito-{Guid.NewGuid():N}";
        await SeedAsync(ctx, nome);

        await using var verifyCtx = fixture.CreateContext();
        var resultado = await Repo(verifyCtx).ObterPorNomeAsync(nome[..^5]);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task ObterPorNomeAsync_NomeInexistente_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();

        var resultado = await Repo(ctx).ObterPorNomeAsync($"Inexistente-{Guid.NewGuid():N}");

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task ObterPorIdAsync_Encontrado_RetornaGrupo()
    {
        await using var ctx = fixture.CreateContext();
        var grupo = await SeedAsync(ctx, $"Costas-{Guid.NewGuid():N}");

        await using var verifyCtx = fixture.CreateContext();
        var resultado = await Repo(verifyCtx).ObterPorIdAsync(grupo.Id);

        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(grupo.Id);
    }

    [Fact]
    public async Task ObterPorIdAsync_NaoEncontrado_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();

        var resultado = await Repo(ctx).ObterPorIdAsync(Guid.NewGuid());

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task ListarTodosAsync_OrdenaPorNomeAscendente()
    {
        await using var ctx = fixture.CreateContext();
        var prefixo = $"Zzz-{Guid.NewGuid():N}";
        await SeedAsync(ctx, $"{prefixo}-Triceps");
        await SeedAsync(ctx, $"{prefixo}-Biceps");
        await SeedAsync(ctx, $"{prefixo}-Ombro");

        await using var verifyCtx = fixture.CreateContext();
        var resultado = await Repo(verifyCtx).ListarTodosAsync();
        var subset = resultado.Where(g => g.Nome.StartsWith(prefixo)).Select(g => g.Nome).ToList();

        subset.Should().Equal(
            $"{prefixo}-Biceps",
            $"{prefixo}-Ombro",
            $"{prefixo}-Triceps");
    }

    [Fact]
    public async Task ContarAsync_RefleteContagemAposAdicionar()
    {
        await using var baselineCtx = fixture.CreateContext();
        var baseline = await Repo(baselineCtx).ContarAsync();

        await using var seedCtx = fixture.CreateContext();
        await SeedAsync(seedCtx, $"Panturrilha-{Guid.NewGuid():N}");
        await SeedAsync(seedCtx, $"Antebraco-{Guid.NewGuid():N}");

        await using var verifyCtx = fixture.CreateContext();
        var contagem = await Repo(verifyCtx).ContarAsync();

        contagem.Should().Be(baseline + 2);
    }

    [Fact]
    public async Task AdicionarAsync_AposSaveChanges_RegistroFicaRecuperavel()
    {
        await using var ctx = fixture.CreateContext();
        var grupo = GrupoMuscular.Criar($"Abdomen-{Guid.NewGuid():N}", DateTime.UtcNow).Value;

        await Repo(ctx).AdicionarAsync(grupo);
        await ctx.SaveChangesAsync();

        await using var verifyCtx = fixture.CreateContext();
        var persistido = await Repo(verifyCtx).ObterPorIdAsync(grupo.Id);

        persistido.Should().NotBeNull();
        persistido!.Nome.Should().Be(grupo.Nome);
    }

    [Fact]
    public async Task Excluir_AposSaveChanges_RegistroDeixaDeExistir()
    {
        await using var ctx = fixture.CreateContext();
        var grupo = await SeedAsync(ctx, $"Lombar-{Guid.NewGuid():N}");

        await using var deleteCtx = fixture.CreateContext();
        var paraExcluir = await Repo(deleteCtx).ObterPorIdAsync(grupo.Id);
        Repo(deleteCtx).Excluir(paraExcluir!);
        await deleteCtx.SaveChangesAsync();

        await using var verifyCtx = fixture.CreateContext();
        var resultado = await Repo(verifyCtx).ObterPorIdAsync(grupo.Id);

        resultado.Should().BeNull();
    }
}
