using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace forzion.tech.Tests.Infrastructure;

[CollectionDefinition(Name)]
public class InfrastructureTestCollection : ICollectionFixture<InfrastructureTestFixture>
{
    public const string Name = "Infrastructure";
}

public sealed class InfrastructureTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithDatabase("forzion_test")
        .WithUsername("test")
        .WithPassword("test")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    // Schema "homolog": o EF cacheia o IModel por tipo de DbContext (a chave de
    // cache não inclui o schema). Todos os contexts de integração do assembly usam
    // o MESMO schema (homolog, o real do deploy) pra o model cacheado ficar
    // consistente entre estas fixtures e a E2E — senão um model de schema diferente
    // vaza pro outro no mesmo processo.
    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options, schema: "homolog");
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
