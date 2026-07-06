using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class PlanoPlataformaRepositoryTests(InfrastructureTestFixture fixture)
{
    private static PlanoPlataformaRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<PlanoPlataforma> SeedAsync(AppDbContext ctx, string nome, TierPlano tier, decimal preco, bool ativo = true)
    {
        var plano = PlanoPlataforma.Criar(nome, tier, 10, preco, DateTime.UtcNow).Value;
        if (!ativo)
            plano.Inativar(DateTime.UtcNow);
        await ctx.Set<PlanoPlataforma>().AddAsync(plano);
        await ctx.SaveChangesAsync();
        return plano;
    }

    [Fact]
    public async Task ObterPlanoFreeAsync_ComMultiplosFreeAtivos_RetornaOMaisBarato()
    {
        await using var ctx = fixture.CreateContext();
        var nomeBase = Guid.NewGuid().ToString("N");
        var maisBarato = await SeedAsync(ctx, $"Free-{nomeBase}-A", TierPlano.Free, 0m);
        await SeedAsync(ctx, $"Free-{nomeBase}-B", TierPlano.Free, 5m);

        await using var verifyCtx = fixture.CreateContext();
        var resultado = await Repo(verifyCtx).ObterPlanoFreeAsync();

        resultado.Should().NotBeNull();
        resultado!.Preco.Should().Be(0m);
        resultado.Tier.Should().Be(TierPlano.Free);
    }

    [Fact]
    public async Task ObterPlanoFreeAsync_FreeInativo_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var nome = $"Free-{Guid.NewGuid():N}";
        await SeedAsync(ctx, nome, TierPlano.Free, 0m, ativo: false);

        await using var verifyCtx = fixture.CreateContext();
        var resultado = await Repo(verifyCtx).ObterPlanoFreeAsync();

        (resultado is null || resultado.Nome != nome).Should().BeTrue();
    }

    [Fact]
    public async Task ObterPlanoFreeAsync_IgnoraTiersPagos()
    {
        await using var ctx = fixture.CreateContext();
        var nome = $"Pro-{Guid.NewGuid():N}";
        await SeedAsync(ctx, nome, TierPlano.Pro, 99m);

        await using var verifyCtx = fixture.CreateContext();
        var resultado = await Repo(verifyCtx).ObterPlanoFreeAsync();

        (resultado is null || resultado.Nome != nome).Should().BeTrue();
    }
}
