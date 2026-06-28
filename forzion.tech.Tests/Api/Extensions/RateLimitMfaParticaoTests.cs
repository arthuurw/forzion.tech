using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using forzion.tech.Application.Auth;
using forzion.tech.Application.UseCases.Auth.Mfa;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace forzion.tech.Tests.Api.Extensions;

public class RateLimitMfaParticaoTests : IClassFixture<RateLimitMfaParticaoTests.MfaLimiterFactory>
{
    private readonly MfaLimiterFactory _factory;

    public RateLimitMfaParticaoTests(MfaLimiterFactory factory) => _factory = factory;

    [Fact]
    public async Task VerificarMfa_SeisChamadasDaMesmaConta_RetornaTooManyRequestsNaSexta()
    {
        var client = _factory.CreateClient();
        var sub = Guid.NewGuid().ToString();
        var status = new List<HttpStatusCode>();

        for (var i = 0; i < 6; i++)
        {
            using var resp = await client.SendAsync(Requisicao(sub, ip: "203.0.113.10"));
            status.Add(resp.StatusCode);
        }

        status.Take(5).Should().NotContain(HttpStatusCode.TooManyRequests);
        status[5].Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task VerificarMfa_RotacaoDeIpComMesmoSub_NaoReiniciaOBucket()
    {
        var client = _factory.CreateClient();
        var sub = Guid.NewGuid().ToString();
        var status = new List<HttpStatusCode>();

        for (var i = 0; i < 6; i++)
        {
            using var resp = await client.SendAsync(Requisicao(sub, ip: $"203.0.113.{20 + i}"));
            status.Add(resp.StatusCode);
        }

        status[5].Should().Be(HttpStatusCode.TooManyRequests);
    }

    private static HttpRequestMessage Requisicao(string sub, string ip)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/auth/mfa/verificar")
        {
            Content = JsonContent.Create(new { codigo = "", fator = 0 }),
        };
        req.Headers.Add("X-Test-Sub", sub);
        req.Headers.Add("X-Test-Ip", ip);
        return req;
    }

    public class MfaLimiterFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");
            builder.UseSetting("RateLimiting:DesabilitarParaTeste", "false");

            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, MfaPendenteAuthHandler>("Test", _ => { });
                services.AddTransient<IStartupFilter, RemoteIpDeHeaderStartupFilter>();
                services.RemoveAll<CompletarLoginMfaHandler>();
                services.AddScoped<CompletarLoginMfaHandler, StubCompletarLoginMfaHandler>();
            });
        }
    }

    private sealed class MfaPendenteAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public MfaPendenteAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var sub = Request.Headers["X-Test-Sub"].FirstOrDefault();
            if (string.IsNullOrEmpty(sub))
                return Task.FromResult(AuthenticateResult.Fail("Sem sub"));

            var claims = new[]
            {
                new Claim("sub", sub),
                new Claim(MfaScopes.ClaimType, MfaScopes.Pendente),
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class RemoteIpDeHeaderStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (ctx, proximo) =>
            {
                var ip = ctx.Request.Headers["X-Test-Ip"].FirstOrDefault();
                if (!string.IsNullOrEmpty(ip))
                    ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);
                await proximo();
            });
            next(app);
        };
    }

    private sealed class StubCompletarLoginMfaHandler : CompletarLoginMfaHandler
    {
        public StubCompletarLoginMfaHandler()
            : base(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!)
        {
        }

        public override Task<Result<CompletarLoginMfaResult>> HandleAsync(CompletarLoginMfaCommand command, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Failure<CompletarLoginMfaResult>(MfaErrors.NaoHabilitado));
    }
}
