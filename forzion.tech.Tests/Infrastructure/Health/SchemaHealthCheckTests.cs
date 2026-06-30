using FluentAssertions;
using forzion.tech.Infrastructure.Health;
using forzion.tech.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace forzion.tech.Tests.Infrastructure.Health;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class SchemaHealthCheckTests(InfrastructureTestFixture fixture)
{
    private static readonly HealthCheckContext FakeCtx = new()
    {
        Registration = new HealthCheckRegistration("schema", _ => null!, null, null)
    };

    private string ConnComSearchPath(string schema) =>
        new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { SearchPath = schema }.ToString();

    private static IConfiguration ConfigCom(string connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AppConnection"] = connectionString
            })
            .Build();

    [Fact]
    public async Task CurrentSchemaIgualEsperado_RetornaHealthy()
    {
        var connectionString = ConnComSearchPath("public");
        await using var ctx = fixture.CreateContext(connectionString);
        var check = new SchemaHealthCheck(ctx, ConfigCom(connectionString));

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CurrentSchemaCaseDiferente_RetornaHealthy()
    {
        var connectionString = ConnComSearchPath("public");
        await using var ctx = fixture.CreateContext(connectionString);
        var check = new SchemaHealthCheck(ctx, ConfigCom(ConnComSearchPath("PUBLIC")));

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CurrentSchemaDivergente_RetornaUnhealthy()
    {
        var connectionString = ConnComSearchPath("public");
        await using var ctx = fixture.CreateContext(connectionString);
        await ctx.Database.OpenConnectionAsync();
        await ctx.Database.ExecuteSqlRawAsync("SET search_path TO pg_catalog");
        var check = new SchemaHealthCheck(ctx, ConfigCom(connectionString));

        var result = await check.CheckHealthAsync(FakeCtx);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("pg_catalog");
    }
}
