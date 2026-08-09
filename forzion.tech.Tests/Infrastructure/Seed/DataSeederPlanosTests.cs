using FluentAssertions;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Seed;
using forzion.tech.Infrastructure.Services;
using forzion.tech.Tests.Infrastructure;
using forzion.tech.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Seed;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class DataSeederPlanosTests(InfrastructureTestFixture fixture)
{
    [Fact]
    public async Task SeedAsync_PlanoElite_CriadoInativo()
    {
        var connectionString = await SeedAsync();

        await using var ctx = fixture.CreateContext(connectionString);
        var elite = await ctx.PlanosPlataforma.SingleAsync(p => p.Tier == TierPlano.Elite);
        elite.IsAtivo.Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_PlanoProPlus_CriadoInativo()
    {
        var connectionString = await SeedAsync();

        await using var ctx = fixture.CreateContext(connectionString);
        var proPlus = await ctx.PlanosPlataforma.SingleAsync(p => p.Tier == TierPlano.ProPlus);
        proPlus.IsAtivo.Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_PlanosVendaveis_PermanecemAtivos()
    {
        var connectionString = await SeedAsync();

        await using var ctx = fixture.CreateContext(connectionString);
        var vendaveis = await ctx.PlanosPlataforma
            .Where(p => p.Tier != TierPlano.Elite && p.Tier != TierPlano.ProPlus)
            .ToListAsync();
        vendaveis.Should().NotBeEmpty();
        vendaveis.Should().OnlyContain(p => p.IsAtivo);
    }

    [Fact]
    public async Task SeedAsync_TodosPlanos_TemDescricaoPreenchida()
    {
        var connectionString = await SeedAsync();

        await using var ctx = fixture.CreateContext(connectionString);
        var todos = await ctx.PlanosPlataforma.ToListAsync();
        todos.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Descricao));
    }

    private async Task<string> SeedAsync()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminPassword"] = "Admin#Senha123",
                ["Seed:ZapTestPassword"] = "Zap#Senha123",
            })
            .Build();

        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == "Homolog");
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero));

        var connectionString = await fixture.CriarBancoIsoladoAsync();
        await using var ctx = fixture.CreateContext(connectionString);
        var seeder = new DataSeeder(
            ctx, new BcryptPasswordHasher(), config, env, time, NullLogger<DataSeeder>.Instance,
            new FakePwnedPasswordsService());

        await seeder.SeedAsync();
        return connectionString;
    }
}
