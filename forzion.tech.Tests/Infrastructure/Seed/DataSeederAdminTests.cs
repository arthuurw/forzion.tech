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
public class DataSeederAdminTests(InfrastructureTestFixture fixture)
{
    private async Task<DataSeeder> SeederEmDbFrescoAsync(string connectionString, string? adminPassword, bool senhaComprometida = false)
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
            ctx, new BcryptPasswordHasher(), config, env, time, NullLogger<DataSeeder>.Instance,
            new FakePwnedPasswordsService(senhaComprometida));
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

    [Theory]
    [InlineData("curta1A")] // < 12 caracteres
    [InlineData("semdigitoemsenha")] // sem dígito
    [InlineData("semmaiuscula123")] // sem maiúscula
    [InlineData("SEMMINUSCULA123")] // sem minúscula
    public async Task SeedAsync_AdminPasswordFraco_LancaENaoCriaSuperAdmin(string senha)
    {
        var connectionString = await fixture.CriarBancoIsoladoAsync();
        var seeder = await SeederEmDbFrescoAsync(connectionString, senha);

        var act = async () => await seeder.SeedAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
        await using var verify = fixture.CreateContext(connectionString);
        (await verify.SystemUsers.AnyAsync(u => u.Role == SystemRole.SuperAdmin))
            .Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_AdminPasswordComprometidaNoHibp_LancaENaoCriaSuperAdmin()
    {
        var connectionString = await fixture.CriarBancoIsoladoAsync();
        var seeder = await SeederEmDbFrescoAsync(connectionString, "Admin#Senha123", senhaComprometida: true);

        var act = async () => await seeder.SeedAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
        await using var verify = fixture.CreateContext(connectionString);
        (await verify.SystemUsers.AnyAsync(u => u.Role == SystemRole.SuperAdmin))
            .Should().BeFalse();
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

    [Fact]
    public async Task SeedAsync_SuperAdminJaExiste_NaoRevalidaSenha()
    {
        var connectionString = await fixture.CriarBancoIsoladoAsync();
        var primeiroSeeder = await SeederEmDbFrescoAsync(connectionString, "Admin#Senha123");
        await primeiroSeeder.SeedAsync();

        // 2ª rodada com senha fraca: se a validação rodasse de novo, lançaria — mas o
        // guard `jaExiste` deve interceptar antes de qualquer checagem de força.
        var segundoSeeder = await SeederEmDbFrescoAsync(connectionString, "fraca");

        var act = async () => await segundoSeeder.SeedAsync();

        await act.Should().NotThrowAsync();
    }
}
