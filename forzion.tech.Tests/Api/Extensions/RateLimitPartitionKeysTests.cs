using System.Net;
using System.Security.Claims;
using FluentAssertions;
using forzion.tech.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace forzion.tech.Tests.Api.Extensions;

public class RateLimitPartitionKeysTests
{
    [Fact]
    public void KeyFromIpOrSub_ComClaimSub_ParticionaPorConta()
    {
        var ctx = ContextoCom(subClaim: "abc-123", ip: "203.0.113.7");

        RateLimitPartitionKeys.KeyFromIpOrSub(ctx).Should().Be("u:abc-123");
    }

    [Fact]
    public void KeyFromIpOrSub_ComNameIdentifierSemSub_ParticionaPorConta()
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "nid-9") }, "Test"));
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        RateLimitPartitionKeys.KeyFromIpOrSub(ctx).Should().Be("u:nid-9");
    }

    [Fact]
    public void KeyFromIpOrSub_SemToken_CaiParaIp()
    {
        var ctx = ContextoCom(subClaim: null, ip: "203.0.113.7");

        RateLimitPartitionKeys.KeyFromIpOrSub(ctx).Should().Be("ip:203.0.113.7");
    }

    [Fact]
    public void KeyFromIpOrSub_SemTokenSemIp_RetornaIpUnknown()
    {
        var ctx = ContextoCom(subClaim: null, ip: null);

        RateLimitPartitionKeys.KeyFromIpOrSub(ctx).Should().Be("ip:unknown");
    }

    [Fact]
    public void KeyFromIp_ComSubPresente_IgnoraSubEUsaIp()
    {
        var ctx = ContextoCom(subClaim: "abc-123", ip: "203.0.113.7");

        RateLimitPartitionKeys.KeyFromIp(ctx).Should().Be("ip:203.0.113.7");
    }

    private static DefaultHttpContext ContextoCom(string? subClaim, string? ip)
    {
        var ctx = new DefaultHttpContext();
        if (subClaim is not null)
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim("sub", subClaim) }, "Test"));
        if (ip is not null)
            ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return ctx;
    }
}
