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
public class AdicionarLeadsMigrationTests(InfrastructureTestFixture fixture)
{
    private const string MigracaoAnterior = "20260822132646_AdicionarPerfilPublicoECatalogoAgentes";

    private static async Task<string> CriarBancoVazioAsync(string connectionStringAdmin)
    {
        var dbName = "mig_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connectionStringAdmin);
        await admin.OpenAsync();
        await using var create = admin.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{dbName}\"";
        await create.ExecuteNonQueryAsync();

        return new NpgsqlConnectionStringBuilder(connectionStringAdmin) { Database = dbName }.ToString();
    }

    private static DbContextOptions<AppDbContext> OptionsFor(string connectionString) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

    [Fact]
    public async Task MigrateAsync_SchemaVazio_CriaTabelasDeLeadsComIndicesEsperados()
    {
        var connectionString = await CriarBancoVazioAsync(fixture.ConnectionString);

        await using (var db = new AppDbContext(OptionsFor(connectionString)))
        {
            await db.Database.MigrateAsync();
        }

        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();

        await using var tabelasCmd = verify.CreateCommand();
        tabelasCmd.CommandText = @"SELECT string_agg(table_name, ',' ORDER BY table_name)
                                   FROM information_schema.tables
                                   WHERE table_schema='public' AND table_name IN ('leads','lead_convites','lead_interacoes')";
        var tabelas = (string?)await tabelasCmd.ExecuteScalarAsync();
        tabelas.Should().Be("lead_convites,lead_interacoes,leads");

        await using var fkAlunoCmd = verify.CreateCommand();
        fkAlunoCmd.CommandText = @"SELECT confdeltype FROM pg_constraint WHERE conname = 'fk_leads_alunos_aluno_id'";
        var fkAluno = (char)(await fkAlunoCmd.ExecuteScalarAsync())!;
        fkAluno.Should().Be('r');

        await using var fkTreinadorCmd = verify.CreateCommand();
        fkTreinadorCmd.CommandText = @"SELECT confdeltype FROM pg_constraint WHERE conname = 'fk_leads_treinadores_treinador_id'";
        var fkTreinador = (char)(await fkTreinadorCmd.ExecuteScalarAsync())!;
        fkTreinador.Should().Be('r');

        await using var fkLeadConviteCmd = verify.CreateCommand();
        fkLeadConviteCmd.CommandText = @"SELECT confdeltype FROM pg_constraint WHERE conname = 'fk_lead_convites_leads_lead_id'";
        var fkLeadConvite = (char)(await fkLeadConviteCmd.ExecuteScalarAsync())!;
        fkLeadConvite.Should().Be('c');

        await using var fkLeadInteracaoCmd = verify.CreateCommand();
        fkLeadInteracaoCmd.CommandText = @"SELECT confdeltype FROM pg_constraint WHERE conname = 'fk_lead_interacoes_leads_lead_id'";
        var fkLeadInteracao = (char)(await fkLeadInteracaoCmd.ExecuteScalarAsync())!;
        fkLeadInteracao.Should().Be('c');

        await using var uniqueTokenCmd = verify.CreateCommand();
        uniqueTokenCmd.CommandText = @"SELECT indexdef FROM pg_indexes WHERE tablename = 'lead_convites' AND indexname = 'ix_lead_convites_token_hash'";
        var uniqueTokenIdx = (string?)await uniqueTokenCmd.ExecuteScalarAsync();
        uniqueTokenIdx.Should().Contain("UNIQUE");

        await using var uniqueIdempotencyCmd = verify.CreateCommand();
        uniqueIdempotencyCmd.CommandText = @"SELECT indexdef FROM pg_indexes WHERE tablename = 'leads' AND indexname = 'ix_leads_treinador_id_idempotency_key_unique'";
        var uniqueIdempotencyIdx = (string?)await uniqueIdempotencyCmd.ExecuteScalarAsync();
        uniqueIdempotencyIdx.Should().Contain("UNIQUE").And.Contain("idempotency_key IS NOT NULL");
    }

    [Fact]
    public async Task MigrateAsync_Down_RemoveTabelasDeLeads()
    {
        var connectionString = await CriarBancoVazioAsync(fixture.ConnectionString);

        await using (var db = new AppDbContext(OptionsFor(connectionString)))
        {
            await db.Database.MigrateAsync();
            var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(MigracaoAnterior);
        }

        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();

        await using var tabelasCmd = verify.CreateCommand();
        tabelasCmd.CommandText = @"SELECT EXISTS (
                                     SELECT 1 FROM information_schema.tables
                                     WHERE table_schema='public' AND table_name IN ('leads','lead_convites','lead_interacoes'))";
        var existeAlguma = (bool)(await tabelasCmd.ExecuteScalarAsync())!;
        existeAlguma.Should().BeFalse();
    }
}
