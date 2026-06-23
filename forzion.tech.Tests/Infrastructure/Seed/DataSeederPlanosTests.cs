using FluentAssertions;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Seed;
using forzion.tech.Infrastructure.Services;
using forzion.tech.Tests.Infrastructure;
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
    public async Task SeedAsync_PlanosNaoElite_PermanecemAtivos()
    {
        var connectionString = await SeedAsync();

        await using var ctx = fixture.CreateContext(connectionString);
        var demais = await ctx.PlanosPlataforma.Where(p => p.Tier != TierPlano.Elite).ToListAsync();
        demais.Should().NotBeEmpty();
        demais.Should().OnlyContain(p => p.IsAtivo);
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
            ctx, new BcryptPasswordHasher(), config, env, time, NullLogger<DataSeeder>.Instance);

        await seeder.SeedAsync();
        return connectionString;
    }
}
