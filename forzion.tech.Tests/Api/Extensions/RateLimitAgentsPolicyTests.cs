using System.Net;
using FluentAssertions;
using forzion.tech.Api.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace forzion.tech.Tests.Api.Extensions;

public class RateLimitAgentsPolicyTests
{
    private const int CapAgentsPorMinuto = 120;
    private const string HeaderIpDeTeste = "X-Test-Ip";
    private const string RotaSonda = "/sonda";

    private sealed record Servidor(WebApplication App, HttpClient Cliente) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Cliente.Dispose();
            await App.DisposeAsync();
        }
    }

    private static async Task<Servidor> IniciarAsync(bool limitersReais)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "*",
            ["Auth:JwtSecret"] = "segredo-de-teste-com-pelo-menos-32-bytes!!",
            ["RateLimiting:DesabilitarParaTeste"] = limitersReais ? "false" : "true",
        });
        builder.Services.AddApiServices(builder.Configuration, builder.Environment);

        var app = builder.Build();
        app.Use(async (contexto, proximo) =>
        {
            var ip = contexto.Request.Headers[HeaderIpDeTeste].FirstOrDefault();
            if (!string.IsNullOrEmpty(ip))
                contexto.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            await proximo();
        });
        app.UseRateLimiter();
        app.MapGet(RotaSonda, () => Results.Ok()).RequireRateLimiting("agents");

        await app.StartAsync();
        return new Servidor(app, app.GetTestClient());
    }

    private static async Task<HttpStatusCode> EnviarAsync(HttpClient cliente, string ip)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, RotaSonda);
        requisicao.Headers.Add(HeaderIpDeTeste, ip);
        using var resposta = await cliente.SendAsync(requisicao);
        return resposta.StatusCode;
    }

    private static async Task<List<HttpStatusCode>> RajadaAsync(HttpClient cliente, string ip, int quantidade)
    {
        var status = new List<HttpStatusCode>();
        for (var i = 0; i < quantidade; i++)
            status.Add(await EnviarAsync(cliente, ip));
        return status;
    }

    [Fact]
    public async Task PolicyAgents_NoRamoDeTesteDoAddRateLimiter_EstaRegistrada()
    {
        await using var servidor = await IniciarAsync(limitersReais: false);

        var status = await EnviarAsync(servidor.Cliente, "203.0.113.1");

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PolicyAgents_CentoEVinteERequisicoesDoMesmoIp_RejeitaAPartirDa121a()
    {
        await using var servidor = await IniciarAsync(limitersReais: true);

        var status = await RajadaAsync(servidor.Cliente, "203.0.113.20", CapAgentsPorMinuto + 1);

        status.Take(CapAgentsPorMinuto).Should().NotContain(HttpStatusCode.TooManyRequests);
        status[CapAgentsPorMinuto].Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task PolicyAgents_BaldeExauridoPorUmIp_NaoAfetaOutroIp()
    {
        await using var servidor = await IniciarAsync(limitersReais: true);

        var doPrimeiro = await RajadaAsync(servidor.Cliente, "203.0.113.30", CapAgentsPorMinuto + 1);
        var doSegundo = await EnviarAsync(servidor.Cliente, "203.0.113.31");

        doPrimeiro[CapAgentsPorMinuto].Should().Be(HttpStatusCode.TooManyRequests);
        doSegundo.Should().Be(HttpStatusCode.OK);
    }
}
