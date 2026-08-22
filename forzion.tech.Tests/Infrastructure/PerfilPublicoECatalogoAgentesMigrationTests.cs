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
public class PerfilPublicoECatalogoAgentesMigrationTests(InfrastructureTestFixture fixture)
{
    private const string MigracaoAnterior = "20260801201209_InativarPlanoProPlus";

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
    public async Task MigrateAsync_SchemaVazio_CriaColunasETabelaHorariosSemUnicidade()
    {
        var connectionString = await CriarBancoVazioAsync(fixture.ConnectionString);

        await using (var db = new AppDbContext(OptionsFor(connectionString)))
        {
            await db.Database.MigrateAsync();
        }

        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();

        await using var perfilColsCmd = verify.CreateCommand();
        perfilColsCmd.CommandText = @"SELECT string_agg(column_name, ',' ORDER BY column_name)
                                      FROM information_schema.columns
                                      WHERE table_schema='public' AND table_name='treinadores' AND column_name LIKE 'perfil_publico%'";
        var perfilCols = (string?)await perfilColsCmd.ExecuteScalarAsync();
        perfilCols.Should().Be("perfil_publico_endereco_bairro,perfil_publico_endereco_cep,perfil_publico_endereco_cidade," +
            "perfil_publico_endereco_complemento,perfil_publico_endereco_estado,perfil_publico_endereco_numero," +
            "perfil_publico_endereco_rua,perfil_publico_is_publicado,perfil_publico_nome_fantasia,perfil_publico_politicas," +
            "perfil_publico_updated_at");

        await using var pacoteColsCmd = verify.CreateCommand();
        pacoteColsCmd.CommandText = @"SELECT string_agg(column_name, ',' ORDER BY column_name)
                                      FROM information_schema.columns
                                      WHERE table_schema='public' AND table_name='pacotes'
                                        AND column_name IN ('categoria','duracao_minutos','trial_disponivel','is_publico')";
        var pacoteCols = (string?)await pacoteColsCmd.ExecuteScalarAsync();
        pacoteCols.Should().Be("categoria,duracao_minutos,is_publico,trial_disponivel");

        await using var tableExistsCmd = verify.CreateCommand();
        tableExistsCmd.CommandText = @"SELECT EXISTS (
                                         SELECT 1 FROM information_schema.tables
                                         WHERE table_schema='public' AND table_name='horarios_funcionamento')";
        var exists = (bool)(await tableExistsCmd.ExecuteScalarAsync())!;
        exists.Should().BeTrue();

        await using var uniqueCmd = verify.CreateCommand();
        uniqueCmd.CommandText = @"SELECT indexdef FROM pg_indexes
                                  WHERE schemaname='public' AND tablename='horarios_funcionamento'
                                    AND indexdef ILIKE '%UNIQUE%' AND indexdef ILIKE '%dia_semana%'";
        var uniqueIdx = (string?)await uniqueCmd.ExecuteScalarAsync();
        uniqueIdx.Should().BeNull("turnos manhã/tarde no mesmo dia da semana são válidos — não há unicidade por (treinador_id, dia_semana)");
    }

    [Fact]
    public async Task MigrateAsync_ComTreinadorEPacotePreExistentes_PreservaDadosEAplicaDefaultsSeguros()
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
            await act.Should().NotThrowAsync("dado pré-existente não pode quebrar a migration");
        }

        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();

        await using var treinadorCmd = verify.CreateCommand();
        treinadorCmd.CommandText = "SELECT nome, perfil_publico_is_publicado FROM treinadores WHERE id = @id";
        treinadorCmd.Parameters.AddWithValue("id", treinadorId);
        await using (var reader = await treinadorCmd.ExecuteReaderAsync())
        {
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetString(0).Should().Be("Treinador Legado");
            reader.GetBoolean(1).Should().BeFalse();
        }

        await using var pacoteCmd = verify.CreateCommand();
        pacoteCmd.CommandText = "SELECT nome, is_publico, trial_disponivel, categoria, duracao_minutos FROM pacotes WHERE id = @id";
        pacoteCmd.Parameters.AddWithValue("id", pacoteId);
        await using (var reader = await pacoteCmd.ExecuteReaderAsync())
        {
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetString(0).Should().Be("Pacote Legado");
            reader.GetBoolean(1).Should().BeFalse();
            reader.GetBoolean(2).Should().BeFalse();
            reader.IsDBNull(3).Should().BeTrue();
            reader.IsDBNull(4).Should().BeTrue();
        }
    }
}
