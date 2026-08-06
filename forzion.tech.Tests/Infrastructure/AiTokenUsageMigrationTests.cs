using FluentAssertions;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace forzion.tech.Tests.Infrastructure;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class AiTokenUsageMigrationTests(InfrastructureTestFixture fixture)
{
    private const string MigracaoAnterior = "20260709195038_RemoverNotasFiscais";

    private static async Task<string> CriarBancoComSchemaAppAsync(string connectionStringAdmin)
    {
        var dbName = "mig_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(connectionStringAdmin))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{dbName}\"";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(connectionStringAdmin)
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

        return connectionString;
    }

    [Fact]
    public async Task MigrateAsync_EmSchemaNaoHomolog_CriaAiTokenUsageComEstruturaEUnique()
    {
        var connectionString = await CriarBancoComSchemaAppAsync(fixture.ConnectionString);

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

    [Fact]
    public async Task MigrateAsync_Down_AposUpCompleto_NaoDropaAiTokenUsage()
    {
        var connectionString = await CriarBancoComSchemaAppAsync(fixture.ConnectionString);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();
            var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(MigracaoAnterior);
        }

        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();
        await using var existsCmd = verify.CreateCommand();
        existsCmd.CommandText = @"SELECT EXISTS (
                                     SELECT 1 FROM information_schema.tables
                                     WHERE table_schema='app' AND table_name='ai_token_usage')";
        var exists = (bool)(await existsCmd.ExecuteScalarAsync())!;
        exists.Should().BeTrue("Down() é no-op — a migration não é dona da tabela nos schemas legados");
    }

    [Fact]
    public async Task MigrateAsync_ComDuplicataPreExistente_DegradaSemAbortarEUniqueNaoCriado()
    {
        var connectionString = await CriarBancoComSchemaAppAsync(fixture.ConnectionString);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using (var db = new AppDbContext(options))
        {
            var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(MigracaoAnterior);
        }

        var userId = Guid.NewGuid();
        await using (var seed = new NpgsqlConnection(connectionString))
        {
            await seed.OpenAsync();
            await using var createLegacy = seed.CreateCommand();
            createLegacy.CommandText = @"
                CREATE TABLE ai_token_usage (
                    id uuid NOT NULL,
                    user_id uuid NOT NULL,
                    agent_type character varying NOT NULL,
                    date date NOT NULL,
                    token_count integer NOT NULL,
                    CONSTRAINT pk_ai_token_usage PRIMARY KEY (id)
                );
                INSERT INTO ai_token_usage (id, user_id, agent_type, date, token_count) VALUES
                    (gen_random_uuid(), @userId, 'coach', '2026-07-01', 10),
                    (gen_random_uuid(), @userId, 'coach', '2026-07-01', 20);";
            createLegacy.Parameters.AddWithValue("userId", userId);
            await createLegacy.ExecuteNonQueryAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var act = async () => await db.Database.MigrateAsync();
            await act.Should().NotThrowAsync("duplicatas pré-existentes devem degradar para RAISE NOTICE, não abortar o migrate");
        }

        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();

        await using var countCmd = verify.CreateCommand();
        countCmd.CommandText = "SELECT count(*) FROM ai_token_usage WHERE user_id = @userId";
        countCmd.Parameters.AddWithValue("userId", userId);
        var count = (long)(await countCmd.ExecuteScalarAsync())!;
        count.Should().Be(2, "o migrate não deve apagar/alterar dado pré-existente");

        await using var idxCmd = verify.CreateCommand();
        idxCmd.CommandText = @"SELECT indexdef FROM pg_indexes
                               WHERE schemaname='app' AND indexname='ix_ai_token_usage_user_agent_date'";
        var indexDef = (string?)await idxCmd.ExecuteScalarAsync();
        indexDef.Should().BeNull("o unique index não pode ser criado sobre duplicatas pré-existentes");
    }
}
