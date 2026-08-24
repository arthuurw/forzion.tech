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
public class AdicionarSolicitacoesAgendamentoMigrationTests(InfrastructureTestFixture fixture)
{
    private const string MigracaoAnterior = "20260823191435_AdicionarAgenda";

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
    public async Task MigrateAsync_SchemaVazio_CriaTabelaSolicitacoesAgendamentoComColunasNulabilidadeIndicesEFksEsperados()
    {
        var connectionString = await CriarBancoVazioAsync(fixture.ConnectionString);

        await using (var db = new AppDbContext(OptionsFor(connectionString)))
        {
            await db.Database.MigrateAsync();
        }

        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();

        await using var tableExistsCmd = verify.CreateCommand();
        tableExistsCmd.CommandText = @"SELECT EXISTS (
                                         SELECT 1 FROM information_schema.tables
                                         WHERE table_schema='public' AND table_name='solicitacoes_agendamento')";
        var exists = (bool)(await tableExistsCmd.ExecuteScalarAsync())!;
        exists.Should().BeTrue();

        await using var colsCmd = verify.CreateCommand();
        colsCmd.CommandText = @"SELECT string_agg(column_name, ',' ORDER BY column_name)
                                 FROM information_schema.columns
                                 WHERE table_schema='public' AND table_name='solicitacoes_agendamento'";
        var cols = (string?)await colsCmd.ExecuteScalarAsync();
        cols.Should().Be("argumentos_hash,created_at,decidida_em,decidida_por_id,fim_utc,id,idempotency_key,inicio_utc,lead_id,motivo,pacote_id,slot_id,status,treinador_id,updated_at");

        await using var naoNulasCmd = verify.CreateCommand();
        naoNulasCmd.CommandText = @"SELECT string_agg(column_name, ',' ORDER BY column_name)
                                     FROM information_schema.columns
                                     WHERE table_schema='public' AND table_name='solicitacoes_agendamento' AND is_nullable='NO'";
        var naoNulas = (string?)await naoNulasCmd.ExecuteScalarAsync();
        naoNulas.Should().Be("argumentos_hash,created_at,fim_utc,id,idempotency_key,inicio_utc,lead_id,pacote_id,slot_id,status,treinador_id");

        await using var nulasCmd = verify.CreateCommand();
        nulasCmd.CommandText = @"SELECT string_agg(column_name, ',' ORDER BY column_name)
                                  FROM information_schema.columns
                                  WHERE table_schema='public' AND table_name='solicitacoes_agendamento' AND is_nullable='YES'";
        var nulas = (string?)await nulasCmd.ExecuteScalarAsync();
        nulas.Should().Be("decidida_em,decidida_por_id,motivo,updated_at");

        await using var fkTreinadorCmd = verify.CreateCommand();
        fkTreinadorCmd.CommandText = @"SELECT confdeltype FROM pg_constraint WHERE conname = 'fk_solicitacoes_agendamento_treinadores_treinador_id'";
        var fkTreinador = (char)(await fkTreinadorCmd.ExecuteScalarAsync())!;
        fkTreinador.Should().Be('c');

        await using var fkPacoteCmd = verify.CreateCommand();
        fkPacoteCmd.CommandText = @"SELECT confdeltype FROM pg_constraint WHERE conname = 'fk_solicitacoes_agendamento_pacotes_pacote_id'";
        var fkPacote = (char)(await fkPacoteCmd.ExecuteScalarAsync())!;
        fkPacote.Should().Be('r');

        await using var fkLeadCmd = verify.CreateCommand();
        fkLeadCmd.CommandText = @"SELECT confdeltype FROM pg_constraint WHERE conname = 'fk_solicitacoes_agendamento_leads_lead_id'";
        var fkLead = (char)(await fkLeadCmd.ExecuteScalarAsync())!;
        fkLead.Should().Be('r');

        await using var idxUniqueCmd = verify.CreateCommand();
        idxUniqueCmd.CommandText = @"SELECT indexdef FROM pg_indexes
                                      WHERE tablename = 'solicitacoes_agendamento'
                                        AND indexname = 'ix_solicitacoes_agendamento_treinador_id_idempotency_key_unique'";
        var idxUnique = (string?)await idxUniqueCmd.ExecuteScalarAsync();
        idxUnique.Should().Contain("UNIQUE").And.Contain("treinador_id").And.Contain("idempotency_key");

        await using var idxCapacidadeCmd = verify.CreateCommand();
        idxCapacidadeCmd.CommandText = @"SELECT indexdef FROM pg_indexes
                                          WHERE tablename = 'solicitacoes_agendamento'
                                            AND indexname = 'ix_solicitacoes_agendamento_treinador_id_pacote_id_status_inicio_utc'";
        var idxCapacidade = (string?)await idxCapacidadeCmd.ExecuteScalarAsync();
        idxCapacidade.Should().Contain("treinador_id").And.Contain("pacote_id").And.Contain("status").And.Contain("inicio_utc");

        await using var idxEsteiraCmd = verify.CreateCommand();
        idxEsteiraCmd.CommandText = @"SELECT indexdef FROM pg_indexes
                                       WHERE tablename = 'solicitacoes_agendamento'
                                         AND indexname = 'ix_solicitacoes_agendamento_treinador_id_status_inicio_utc'";
        var idxEsteira = (string?)await idxEsteiraCmd.ExecuteScalarAsync();
        idxEsteira.Should().Contain("treinador_id").And.Contain("status").And.Contain("inicio_utc");
    }

    [Fact]
    public async Task MigrateAsync_SchemaVazio_TabelaNaoTemColunaDeNomeContatoOuConsentimento()
    {
        var connectionString = await CriarBancoVazioAsync(fixture.ConnectionString);

        await using (var db = new AppDbContext(OptionsFor(connectionString)))
        {
            await db.Database.MigrateAsync();
        }

        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();

        await using var piiCmd = verify.CreateCommand();
        piiCmd.CommandText = @"SELECT count(*) FROM information_schema.columns
                                WHERE table_schema='public' AND table_name='solicitacoes_agendamento'
                                  AND column_name IN ('nome','contato','contato_tipo','contato_valor',
                                                       'consentimento','consentimento_finalidade',
                                                       'consentimento_concedido_em','email','telefone')";
        var piiCount = (long)(await piiCmd.ExecuteScalarAsync())!;
        piiCount.Should().Be(0);
    }

    [Fact]
    public async Task MigrateAsync_Down_RemoveTabelaSolicitacoesAgendamento()
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

        await using var tabelaCmd = verify.CreateCommand();
        tabelaCmd.CommandText = @"SELECT EXISTS (
                                    SELECT 1 FROM information_schema.tables
                                    WHERE table_schema='public' AND table_name='solicitacoes_agendamento')";
        var tabelaExiste = (bool)(await tabelaCmd.ExecuteScalarAsync())!;
        tabelaExiste.Should().BeFalse();
    }
}
