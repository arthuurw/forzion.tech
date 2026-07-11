using FluentAssertions;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace forzion.tech.Tests.Infrastructure;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class AiTokenUsageMigrationTests(InfrastructureTestFixture fixture)
{
    [Fact]
    public async Task MigrateAsync_EmSchemaNaoHomolog_CriaAiTokenUsageComEstruturaEUnique()
    {
        var dbName = "mig_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(fixture.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{dbName}\"";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = dbName,
            SearchPath = "app",
        }.ToString();

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            await using var schema = conn.CreateCommand();
            schema.CommandText = "CREATE SCHEMA IF NOT EXISTS app;";
            await schema.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();

        await using var colsCmd = verify.CreateCommand();
        colsCmd.CommandText = @"SELECT string_agg(column_name || ':' || data_type, ',' ORDER BY ordinal_position)
                                FROM information_schema.columns
                                WHERE table_schema='app' AND table_name='ai_token_usage'";
        var cols = (string?)await colsCmd.ExecuteScalarAsync();
        cols.Should().Be("id:uuid,user_id:uuid,agent_type:character varying,date:date,token_count:integer");

        await using var idxCmd = verify.CreateCommand();
        idxCmd.CommandText = @"SELECT indexdef FROM pg_indexes
                               WHERE schemaname='app' AND indexname='ix_ai_token_usage_user_agent_date'";
        var indexDef = (string?)await idxCmd.ExecuteScalarAsync();
        indexDef.Should().Contain("UNIQUE").And.Contain("user_id, agent_type, date");
    }
}
