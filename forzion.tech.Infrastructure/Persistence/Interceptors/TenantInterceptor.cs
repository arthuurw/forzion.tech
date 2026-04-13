using System.Data.Common;
using forzion.tech.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace forzion.tech.Infrastructure.Persistence.Interceptors;

public class TenantInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await SetTenantAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);
        SetTenantAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task SetTenantAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId?.ToString() ?? string.Empty;
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant_id', @id, false)";
        cmd.Parameters.Add(new NpgsqlParameter("@id", tenantId));
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
