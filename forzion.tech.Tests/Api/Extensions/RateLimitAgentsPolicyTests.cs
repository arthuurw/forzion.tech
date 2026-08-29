using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Api.Endpoints.Agents;
using forzion.tech.Api.Endpoints.Agents.Hmac;
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
    private const string Segredo = "segredo-atual-com-pelo-menos-32-bytes!!";
    private const string CaminhoDeEcoDoGrupo = AgentEndpoints.Prefixo + "/eco";

    private sealed record Servidor(WebApplication App, HttpClient Cliente) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Cliente.Dispose();
            await App.DisposeAsync();
        }
    }

    private static async Task<Servidor> IniciarAsync(bool limitersReais, bool montarGrupoRealDeAgentes = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "*",
            ["Auth:JwtSecret"] = "segredo-de-teste-com-pelo-menos-32-bytes!!",
            ["Agents:Hmac:SecretAtual"] = Segredo,
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

        // O grupo REAL da produção, sem `.RequireRateLimiting` acrescentado aqui: remover a
        // anotação de `CriarGrupo` tem que derrubar este teste.
        if (montarGrupoRealDeAgentes)
            AgentEndpoints.CriarGrupo(app).MapGet("/eco", () => Results.Ok());

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

    private static async Task<HttpStatusCode> EnviarAssinadaAoGrupoAsync(HttpClient cliente, string ip)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"GET\n{CaminhoDeEcoDoGrupo}\n{Convert.ToHexStringLower(SHA256.HashData([]))}\n{timestamp}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Segredo), Encoding.UTF8.GetBytes(payload));

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, CaminhoDeEcoDoGrupo);
        requisicao.Headers.Add(HeaderIpDeTeste, ip);
        requisicao.Headers.TryAddWithoutValidation(
            HmacSignatureFilter.HeaderDeAssinatura, "v1=" + Convert.ToHexStringLower(mac));
        requisicao.Headers.TryAddWithoutValidation(
            HmacSignatureFilter.HeaderDeTimestamp, timestamp.ToString(provider: null));

        using var resposta = await cliente.SendAsync(requisicao);
        return resposta.StatusCode;
    }

    private static async Task<HttpResponseMessage> EnviarAssinadaAoGrupoRetornandoRespostaAsync(HttpClient cliente, string ip)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"GET\n{CaminhoDeEcoDoGrupo}\n{Convert.ToHexStringLower(SHA256.HashData([]))}\n{timestamp}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Segredo), Encoding.UTF8.GetBytes(payload));

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, CaminhoDeEcoDoGrupo);
        requisicao.Headers.Add(HeaderIpDeTeste, ip);
        requisicao.Headers.TryAddWithoutValidation(
            HmacSignatureFilter.HeaderDeAssinatura, "v1=" + Convert.ToHexStringLower(mac));
        requisicao.Headers.TryAddWithoutValidation(
            HmacSignatureFilter.HeaderDeTimestamp, timestamp.ToString(provider: null));

        return await cliente.SendAsync(requisicao);
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
    public async Task GrupoRealDeAgentes_CarregaAPolicyAgents_RejeitaAPartirDa121aRequisicaoAssinada()
    {
        await using var servidor = await IniciarAsync(limitersReais: true, montarGrupoRealDeAgentes: true);

        var status = new List<HttpStatusCode>();
        for (var i = 0; i < CapAgentsPorMinuto + 1; i++)
            status.Add(await EnviarAssinadaAoGrupoAsync(servidor.Cliente, "203.0.113.40"));

        status.Take(CapAgentsPorMinuto).Should().AllBeEquivalentTo(HttpStatusCode.OK);
        status[CapAgentsPorMinuto].Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task GrupoRealDeAgentes_Rejeicao429_SaiNoEnvelopeRfc9457ComCodeRateLimited()
    {
        await using var servidor = await IniciarAsync(limitersReais: true, montarGrupoRealDeAgentes: true);

        HttpResponseMessage? ultima = null;
        for (var i = 0; i < CapAgentsPorMinuto + 1; i++)
        {
            ultima?.Dispose();
            ultima = await EnviarAssinadaAoGrupoRetornandoRespostaAsync(servidor.Cliente, "203.0.113.50");
        }

        using var resposta = ultima!;
        resposta.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        documento.RootElement.GetProperty("code").GetString().Should().Be("rate_limited");
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
