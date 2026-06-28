using System.Security.Claims;

namespace forzion.tech.Api.Extensions;

internal static class RateLimitPartitionKeys
{
    internal static string KeyFromIpOrSub(HttpContext ctx)
    {
        var sub = ctx.User?.FindFirst("sub")?.Value
                  ?? ctx.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(sub))
            return $"u:{sub}";
        return $"ip:{ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    internal static string KeyFromIp(HttpContext ctx) =>
        $"ip:{ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}
