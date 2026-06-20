using System.Net;
using FluentAssertions;
using forzion.tech.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace forzion.tech.Tests.Api.Extensions;

public class RateLimitRejeicaoLogTests
{
    [Theory]
    [InlineData("auth")]
    [InlineData("mfa")]
    public void RegistrarRejeicaoAuth_PoliticaSensivel_LogaEventoEstruturadoComIpERota(string politica)
    {
        var coletor = new ColetorLog();
        var ctx = ContextoComPolitica(politica, "/auth/login", "POST", "203.0.113.7");

        DependencyInjectionExtensions.RegistrarRejeicaoAuth(ctx, coletor);

        coletor.Mensagens.Should().ContainSingle();
        coletor.Mensagens[0].Should().ContainAll(politica, "/auth/login", "POST", "203.0.113.7");
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData(null)]
    public void RegistrarRejeicaoAuth_PoliticaNaoSensivel_NaoLoga(string? politica)
    {
        var coletor = new ColetorLog();
        var ctx = ContextoComPolitica(politica, "/admin/x", "GET", "203.0.113.7");

        DependencyInjectionExtensions.RegistrarRejeicaoAuth(ctx, coletor);

        coletor.Mensagens.Should().BeEmpty();
    }

    private static HttpContext ContextoComPolitica(string? politica, string path, string metodo, string ip)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Request.Method = metodo;
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);

        var metadata = politica is null
            ? new EndpointMetadataCollection()
            : new EndpointMetadataCollection(new EnableRateLimitingAttribute(politica));
        ctx.SetEndpoint(new Endpoint(null, metadata, "test"));

        return ctx;
    }

    private sealed class ColetorLog : ILogger
    {
        public List<string> Mensagens { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Mensagens.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
