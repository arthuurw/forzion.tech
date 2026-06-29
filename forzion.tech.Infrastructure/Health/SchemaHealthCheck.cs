using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace forzion.tech.Infrastructure.Health;

public sealed class SchemaHealthCheck(AppDbContext dbContext, IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var esperado = MigrationHistorySchemaResolver.Resolve(
            configuration.GetConnectionString("AppConnection"));

        if (string.IsNullOrWhiteSpace(esperado))
            return HealthCheckResult.Healthy("Search Path não fixado; schema não verificável.");

        var atual = await dbContext.Database
            .SqlQueryRaw<string>("SELECT current_schema() AS \"Value\"")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        return string.Equals(atual, esperado, StringComparison.Ordinal)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy(
                $"current_schema() = '{atual}', esperado '{esperado}' — search_path divergente (possível Transaction pooler :6543).");
    }
}
