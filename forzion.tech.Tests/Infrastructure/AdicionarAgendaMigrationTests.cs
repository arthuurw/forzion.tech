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
public class AdicionarAgendaMigrationTests(InfrastructureTestFixture fixture)
{
    private const string MigracaoAnterior = "20260822170203_AdicionarLeads";

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
    public async Task MigrateAsync_SchemaVazio_CriaColunasETabelaBloqueiosAgendaComIndicesEFkEsperados()
    {
        var connectionString = await CriarBancoVazioAsync(fixture.ConnectionString);

        await using (var db = new AppDbContext(OptionsFor(connectionString)))
        {
            await db.Database.MigrateAsync();
        }

        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();

        await using var treinadorColsCmd = verify.CreateCommand();
        treinadorColsCmd.CommandText = @"SELECT string_agg(column_name, ',' ORDER BY column_name)
                                         FROM information_schema.columns
                                         WHERE table_schema='public' AND table_name='treinadores'
                                           AND column_name IN ('fuso_horario','agenda_antecedencia_minima_horas','agenda_horizonte_dias')";
        var treinadorCols = (string?)await treinadorColsCmd.ExecuteScalarAsync();
        treinadorCols.Should().Be("agenda_antecedencia_minima_horas,agenda_horizonte_dias,fuso_horario");

        await using var fusoNullableCmd = verify.CreateCommand();
        fusoNullableCmd.CommandText = @"SELECT is_nullable FROM information_schema.columns
                                        WHERE table_schema='public' AND table_name='treinadores' AND column_name='fuso_horario'";
        var fusoNullable = (string?)await fusoNullableCmd.ExecuteScalarAsync();
        fusoNullable.Should().Be("NO", "fuso_horario precisa ser NOT NULL — um regressao para nullable:true passaria despercebida sem esta asserção de constraint");

        await using var pacoteColsCmd = verify.CreateCommand();
        pacoteColsCmd.CommandText = @"SELECT column_name FROM information_schema.columns
                                      WHERE table_schema='public' AND table_name='pacotes' AND column_name='capacidade_maxima'";
        var pacoteCol = (string?)await pacoteColsCmd.ExecuteScalarAsync();
        pacoteCol.Should().Be("capacidade_maxima");

        await using var tableExistsCmd = verify.CreateCommand();
        tableExistsCmd.CommandText = @"SELECT EXISTS (
                                         SELECT 1 FROM information_schema.tables
                                         WHERE table_schema='public' AND table_name='bloqueios_agenda')";
        var exists = (bool)(await tableExistsCmd.ExecuteScalarAsync())!;
        exists.Should().BeTrue();

        await using var fkCmd = verify.CreateCommand();
        fkCmd.CommandText = @"SELECT confdeltype FROM pg_constraint WHERE conname = 'fk_bloqueios_agenda_treinadores_treinador_id'";
        var fkDelete = (char)(await fkCmd.ExecuteScalarAsync())!;
        fkDelete.Should().Be('c');

        await using var idxTipoCmd = verify.CreateCommand();
        idxTipoCmd.CommandText = @"SELECT indexdef FROM pg_indexes WHERE tablename = 'bloqueios_agenda' AND indexname = 'ix_bloqueios_agenda_treinador_id_tipo'";
        var idxTipo = (string?)await idxTipoCmd.ExecuteScalarAsync();
        idxTipo.Should().Contain("treinador_id").And.Contain("tipo");

        await using var idxIntervaloCmd = verify.CreateCommand();
        idxIntervaloCmd.CommandText = @"SELECT indexdef FROM pg_indexes WHERE tablename = 'bloqueios_agenda' AND indexname = 'ix_bloqueios_agenda_treinador_id_inicio_utc_fim_utc'";
        var idxIntervalo = (string?)await idxIntervaloCmd.ExecuteScalarAsync();
        idxIntervalo.Should().Contain("treinador_id").And.Contain("inicio_utc").And.Contain("fim_utc");
    }

    [Fact]
    public async Task MigrateAsync_ComTreinadorEPacotePreExistentes_PreservaDadosEAplicaBackfillSemViolarNotNull()
    {
        var connectionString = await CriarBancoVazioAsync(fixture.ConnectionString);
        var contaId = Guid.NewGuid();
        var treinadorId = Guid.NewGuid();
        var pacoteId = Guid.NewGuid();

        await using (var db = new AppDbContext(OptionsFor(connectionString)))
        {
            var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(MigracaoAnterior);
        }

        await using (var seed = new NpgsqlConnection(connectionString))
        {
            await seed.OpenAsync();

            await using var insertConta = seed.CreateCommand();
            insertConta.CommandText = @"INSERT INTO contas (id, email, password_hash, tipo_conta, created_at)
                                         VALUES (@id, @email, 'hash', 'Treinador', now())";
            insertConta.Parameters.AddWithValue("id", contaId);
            insertConta.Parameters.AddWithValue("email", $"t{Guid.NewGuid():N}@test.com");
            await insertConta.ExecuteNonQueryAsync();

            await using var insertTreinador = seed.CreateCommand();
            insertTreinador.CommandText = @"INSERT INTO treinadores (id, conta_id, nome, status, created_at)
                                             VALUES (@id, @contaId, 'Treinador Legado', 'Ativo', now())";
            insertTreinador.Parameters.AddWithValue("id", treinadorId);
            insertTreinador.Parameters.AddWithValue("contaId", contaId);
            await insertTreinador.ExecuteNonQueryAsync();

            await using var insertPacote = seed.CreateCommand();
            insertPacote.CommandText = @"INSERT INTO pacotes (id, treinador_id, nome, preco, is_ativo, created_at)
                                          VALUES (@id, @treinadorId, 'Pacote Legado', 100, true, now())";
            insertPacote.Parameters.AddWithValue("id", pacoteId);
            insertPacote.Parameters.AddWithValue("treinadorId", treinadorId);
            await insertPacote.ExecuteNonQueryAsync();
        }

        await using (var db = new AppDbContext(OptionsFor(connectionString)))
        {
            var act = async () => await db.Database.MigrateAsync();
            await act.Should().NotThrowAsync("dado pré-existente não pode violar as colunas NOT NULL novas");
        }

        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();

        await using var treinadorCmd = verify.CreateCommand();
        treinadorCmd.CommandText = "SELECT nome, fuso_horario, agenda_antecedencia_minima_horas, agenda_horizonte_dias FROM treinadores WHERE id = @id";
        treinadorCmd.Parameters.AddWithValue("id", treinadorId);
        await using (var reader = await treinadorCmd.ExecuteReaderAsync())
        {
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetString(0).Should().Be("Treinador Legado");
            reader.GetString(1).Should().Be("America/Sao_Paulo");
            reader.GetInt32(2).Should().Be(2);
            reader.GetInt32(3).Should().Be(60);
        }

        await using var pacoteCmd = verify.CreateCommand();
        pacoteCmd.CommandText = "SELECT nome, capacidade_maxima FROM pacotes WHERE id = @id";
        pacoteCmd.Parameters.AddWithValue("id", pacoteId);
        await using (var reader = await pacoteCmd.ExecuteReaderAsync())
        {
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetString(0).Should().Be("Pacote Legado");
            reader.GetInt32(1).Should().Be(1);
        }
    }

    [Fact]
    public async Task MigrateAsync_Down_RemoveTabelaEColunasDeAgenda()
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
                                    WHERE table_schema='public' AND table_name='bloqueios_agenda')";
        var tabelaExiste = (bool)(await tabelaCmd.ExecuteScalarAsync())!;
        tabelaExiste.Should().BeFalse();

        await using var colunasCmd = verify.CreateCommand();
        colunasCmd.CommandText = @"SELECT EXISTS (
                                     SELECT 1 FROM information_schema.columns
                                     WHERE table_schema='public' AND table_name='treinadores' AND column_name='fuso_horario')";
        var colunaExiste = (bool)(await colunasCmd.ExecuteScalarAsync())!;
        colunaExiste.Should().BeFalse();
    }
}
