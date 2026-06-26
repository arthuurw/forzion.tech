using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace forzion.tech.Infrastructure.Persistence;

internal sealed class AppRetryingExecutionStrategy(
    ExecutionStrategyDependencies dependencies,
    int maxRetryCount,
    TimeSpan maxRetryDelay)
    : NpgsqlRetryingExecutionStrategy(dependencies, maxRetryCount, maxRetryDelay, errorCodesToAdd: null)
{
    protected override bool ShouldRetryOn(Exception? exception) =>
        !EhConflitoDeSerializacao(exception) && base.ShouldRetryOn(exception);

    internal static bool EhConflitoDeSerializacao(Exception? exception)
    {
        for (var atual = exception; atual is not null; atual = atual.InnerException)
            if (atual is PostgresException pg && pg.SqlState is "40001" or "40P01")
                return true;

        return false;
    }
}
