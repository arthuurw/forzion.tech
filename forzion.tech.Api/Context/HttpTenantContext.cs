using forzion.tech.Application.Interfaces;

namespace forzion.tech.Api.Context;

public class HttpTenantContext : ITenantContext
{
    public Guid? TenantId { get; }

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);

        var claim = httpContextAccessor.HttpContext?.User.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(claim, out var id))
            TenantId = id;
    }
}
