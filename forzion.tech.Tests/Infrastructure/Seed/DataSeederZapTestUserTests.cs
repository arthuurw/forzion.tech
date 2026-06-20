using FluentAssertions;
using forzion.tech.Domain.ValueObjects;
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
public class DataSeederZapTestUserTests(InfrastructureTestFixture fixture)
{
    private async Task SeedAsync(string environmentName, string zapEmail)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminPassword"] = "Admin#Senha123",
                ["Seed:ZapTestPassword"] = "Zap#Senha123",
                ["Seed:ZapTestEmail"] = zapEmail,
            })
            .Build();

        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == environmentName);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero));

        await using var ctx = fixture.CreateContext();
        var seeder = new DataSeeder(
            ctx, new BcryptPasswordHasher(), config, env, time, NullLogger<DataSeeder>.Instance);

        await seeder.SeedAsync();
    }

    [Fact]
    public async Task SeedAsync_EmProducao_NaoCriaContaDeTesteZap()
    {
        var zapEmail = $"zap-prod-{Guid.NewGuid():N}@forzion.tech";

        await SeedAsync("Production", zapEmail);

        await using var ctx = fixture.CreateContext();
        var email = Email.Criar(zapEmail).Value;
        (await ctx.Contas.AnyAsync(c => c.Email == email)).Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_EmHomolog_CriaContaDeTesteZap()
    {
        var zapEmail = $"zap-hmg-{Guid.NewGuid():N}@forzion.tech";

        await SeedAsync("Homolog", zapEmail);

        await using var ctx = fixture.CreateContext();
        var email = Email.Criar(zapEmail).Value;
        (await ctx.Contas.AnyAsync(c => c.Email == email)).Should().BeTrue();
    }
}
