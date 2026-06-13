using forzion.tech.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace forzion.tech.Infrastructure.Persistence;

public sealed class NpgsqlDatabaseErrorInspector : IDatabaseErrorInspector
{
    public bool EhViolacaoDeUnicidade(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // EF embrulha a PostgresException do driver em DbUpdateException no SaveChanges;
        // o ExecuteUpdate cru a propaga direta. 23505 = unique_violation.
        var pg = (exception as PostgresException)
            ?? (exception as DbUpdateException)?.InnerException as PostgresException;

        return pg?.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}
