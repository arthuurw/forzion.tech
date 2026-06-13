using Npgsql;

namespace forzion.tech.Infrastructure.Persistence;

public static class MigrationHistorySchemaResolver
{
    // Npgsql 8.0.11 checa a existência da __EFMigrationsHistory com o schema HARDCODED em "public"
    // quando MigrationsHistoryTable não recebe schema (NpgsqlHistoryRepository.ExistsSql:
    // `n.nspname = TableSchema ?? "public"`), mas o CREATE e a leitura de migrations aplicadas usam o
    // search_path. Num alvo cujo public NÃO tem __EFMigrationsHistory (ex.: dry-run clonando só o
    // schema homolog), o Exists dá falso-negativo → EF emite CREATE plano que cai no search_path e
    // colide com a tabela já existente (42P07). Pinar a history no PRIMEIRO schema do search_path
    // (= current_schema, onde o migrate de fato opera) alinha Exists/CREATE/leitura no mesmo schema.
    // SÓ no runtime (`app migrate`/dry-run); design-time fica unqualified p/ scripts cross-schema
    // portáveis (specification-db §APLICAÇÃO DE MIGRATIONS).
    public static string? Resolve(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var searchPath = new NpgsqlConnectionStringBuilder(connectionString).SearchPath;
        if (string.IsNullOrWhiteSpace(searchPath))
        {
            return null;
        }

        var first = searchPath
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? null : first;
    }
}
