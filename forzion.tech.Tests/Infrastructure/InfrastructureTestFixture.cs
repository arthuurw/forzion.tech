using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    // Modelo é schema-agnostic (sem HasDefaultSchema) — o schema vem do search_path
    // da connection. Estes testes usam o schema default (public) do container; a E2E
    // usa homolog via Search Path. O IModel cacheado não tem schema, então não há
    // vazamento entre fixtures no mesmo processo.
    public AppDbContext CreateContext() => CreateContext(ConnectionString);

    public AppDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }

    public async Task<string> CriarBancoIsoladoAsync()
    {
        var nome = "iso_" + Guid.NewGuid().ToString("N");
        await using (var conn = new NpgsqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{nome}\"";
            await cmd.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = nome }.ToString();
        await using var ctx = CreateContext(connectionString);
        await ctx.Database.EnsureCreatedAsync();
        return connectionString;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
