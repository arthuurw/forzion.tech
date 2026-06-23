using forzion.tech.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace forzion.tech.Infrastructure.Persistence;

public sealed class NpgsqlDatabaseErrorInspector : IDatabaseErrorInspector
{
    public bool EhViolacaoDeUnicidade(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return EncontrarPostgres(exception)?.SqlState == PostgresErrorCodes.UniqueViolation;
    }

    public bool EhConflitoDeSerializacao(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (Exception? atual = exception; atual is not null; atual = atual.InnerException)
        {
            if (atual is DbUpdateConcurrencyException)
                return true;
            if (atual is PostgresException pg)
                return pg.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected;
        }

        return false;
    }

    public bool EhConflitoDeConcorrenciaOtimista(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (Exception? atual = exception; atual is not null; atual = atual.InnerException)
            if (atual is DbUpdateConcurrencyException)
                return true;

        return false;
    }

    private static PostgresException? EncontrarPostgres(Exception exception)
    {
        for (Exception? atual = exception; atual is not null; atual = atual.InnerException)
            if (atual is PostgresException pg)
                return pg;

        return null;
    }
}
