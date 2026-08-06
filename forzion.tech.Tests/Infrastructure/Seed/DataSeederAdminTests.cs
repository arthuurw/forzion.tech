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
public class DataSeederAdminTests(InfrastructureTestFixture fixture)
{
    private async Task<DataSeeder> SeederEmDbFrescoAsync(string connectionString, string? adminPassword)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminPassword"] = adminPassword,
            })
            .Build();

        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == "Production");
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero));
        var ctx = fixture.CreateContext(connectionString);

        return new DataSeeder(
            ctx, new BcryptPasswordHasher(), config, env, time, NullLogger<DataSeeder>.Instance);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SeedAsync_AdminPasswordVazioOuBranco_LancaEmDbFresco(string senha)
    {
        var connectionString = await fixture.CriarBancoIsoladoAsync();
        var seeder = await SeederEmDbFrescoAsync(connectionString, senha);

        var act = async () => await seeder.SeedAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SeedAsync_AdminPasswordValido_CriaSuperAdmin()
    {
        var connectionString = await fixture.CriarBancoIsoladoAsync();
        var seeder = await SeederEmDbFrescoAsync(connectionString, "Admin#Senha123");

        await seeder.SeedAsync();

        await using var verify = fixture.CreateContext(connectionString);
        (await verify.SystemUsers.AnyAsync(u => u.Role == SystemRole.SuperAdmin))
            .Should().BeTrue();
    }
}
