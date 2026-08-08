using System.Net;
using System.Security.Claims;
using FluentAssertions;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace forzion.tech.Tests.Api.Extensions;

public class ForwardedHeadersPipelineTests
{
    private const string RedeDoProxy = "172.18.0.0/16";

    // O middleware só é registrado em Production/Homolog, então WebApplicationFactory (que roda em
    // Test) passaria verde sem exercitar nada. Montar o middleware sobre as options reais é o único
    // arranjo que prova a semântica de peer confiável.
    private static async Task<DefaultHttpContext> ProcessarAsync(string ipDoPeer, string xff, string? subClaim = null)
    {
        var options = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks"] = RedeDoProxy,
            })
            .Build()
            .BuildForwardedHeadersOptions(Mock.Of<IHostEnvironment>(e => e.EnvironmentName == "Production"));

        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            NullLoggerFactory.Instance,
            Options.Create(options));

        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(ipDoPeer);
        ctx.Request.Headers["X-Forwarded-For"] = xff;
        if (subClaim is not null)
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", subClaim) }, "Test"));

        await middleware.Invoke(ctx);
        return ctx;
    }

    [Fact]
    public async Task PeerForaDaRedeConhecida_IgnoraXff_ParticionaPeloIpDoPeer()
    {
        var ctx = await ProcessarAsync(ipDoPeer: "198.51.100.9", xff: "203.0.113.7");

        ctx.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("198.51.100.9"));
        RateLimitPartitionKeys.KeyFromIp(ctx).Should().Be("ip:198.51.100.9");
    }

    [Fact]
    public async Task PeerNaRedeConhecida_AdotaIpDoXff_ParticionaPeloClienteReal()
    {
        var ctx = await ProcessarAsync(ipDoPeer: "172.18.0.5", xff: "203.0.113.7");

        ctx.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("203.0.113.7"));
        RateLimitPartitionKeys.KeyFromIp(ctx).Should().Be("ip:203.0.113.7");
    }

    [Fact]
    public async Task DoisHopsNoXff_ComForwardLimitUm_AdotaOHopDaDireita()
    {
        var ctx = await ProcessarAsync(ipDoPeer: "172.18.0.5", xff: "203.0.113.7, 198.51.100.44");

        ctx.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("198.51.100.44"));
    }

    [Fact]
    public async Task RequisicaoAutenticada_ComXffAdotado_ContinuaParticionandoPorSub()
    {
        var ctx = await ProcessarAsync(ipDoPeer: "172.18.0.5", xff: "203.0.113.7", subClaim: "abc-123");

        RateLimitPartitionKeys.KeyFromIpOrSub(ctx).Should().Be("u:abc-123");
    }
}
