using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using FluentAssertions;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Endpoints.Agents;
using forzion.tech.Api.Endpoints.Agents.Hmac;
using forzion.tech.Api.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace forzion.tech.Tests.Api.Agents;

public class AgentHealthEndpointTests
{
    private const string Segredo = "segredo-atual-com-pelo-menos-32-bytes!!";
    private const string CaminhoDeSaude = AgentEndpoints.Prefixo + "/health";
    private const long TimestampBase = 1_787_000_000;
    private const string NomeDoCheckDeAgentes = "check-de-agentes-com-nome-interno";
    private const string DescricaoDoCheckDeAgentes = "conexao com o postgres do tenant recusada";
    private const string NomeDoCheckForaDaTag = "check-de-integracao-fora-da-tag";
    private const string DescricaoDoCheckForaDaTag = "stripe fora do ar";

    private sealed record Servidor(WebApplication App, HttpClient Cliente) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Cliente.Dispose();
            await App.DisposeAsync();
        }
    }

    private static async Task<Servidor> IniciarAsync(
        HealthStatus statusAgentsReady = HealthStatus.Healthy,
        HealthStatus? statusForaDaTag = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "*",
            ["Agents:Hmac:SecretAtual"] = Segredo,
        });
        builder.Services.AddAgentsHmac(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(TimestampBase)));
        builder.Services.AddSingleton<HmacSignatureVerifier>();
        builder.Services.AddRateLimiter(opt =>
            opt.AddPolicy("agents", _ => RateLimitPartition.GetNoLimiter<string>("test")));

        var checks = builder.Services.AddHealthChecks();
        checks.AddCheck(
            NomeDoCheckDeAgentes,
            () => new HealthCheckResult(statusAgentsReady, DescricaoDoCheckDeAgentes),
            tags: ["ready", AgentEndpoints.TagAgentsReady]);
        if (statusForaDaTag is { } fora)
        {
            checks.AddCheck(
                NomeDoCheckForaDaTag,
                () => new HealthCheckResult(fora, DescricaoDoCheckForaDaTag),
                tags: ["ready"]);
        }

        var app = builder.Build();
        app.UseRateLimiter();
        app.MapAgentEndpoints();

        await app.StartAsync();
        return new Servidor(app, app.GetTestClient());
    }

    private static async Task<HttpResponseMessage> EnviarAssinadaAsync(HttpClient cliente, long timestamp)
    {
        var payload = $"GET\n{CaminhoDeSaude}\n{Convert.ToHexStringLower(SHA256.HashData([]))}\n{timestamp}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Segredo), Encoding.UTF8.GetBytes(payload));

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, CaminhoDeSaude);
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeAssinatura, "v1=" + Convert.ToHexStringLower(mac));
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeTimestamp, timestamp.ToString(provider: null));

        return await cliente.SendAsync(requisicao);
    }

    [Fact]
    public async Task DependenciasSaudaveis_RespondeDuzentosComStatusHealthy()
    {
        await using var servidor = await IniciarAsync(HealthStatus.Healthy);

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, TimestampBase);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("status").GetString().Should().Be("healthy");
    }

    [Fact]
    public async Task DependenciasDegradadas_RespondeDuzentosComStatusDegraded()
    {
        await using var servidor = await IniciarAsync(HealthStatus.Degraded);

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, TimestampBase);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("status").GetString().Should().Be("degraded");
    }

    [Fact]
    public async Task DependenciasNaoSaudaveis_RespondeQuinhentosETresComDependencyUnavailable()
    {
        await using var servidor = await IniciarAsync(HealthStatus.Unhealthy);

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, TimestampBase);

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be("dependency_unavailable");
    }

    [Fact]
    public async Task CheckForaDaTagAgentsReadyReprovando_NaoAlteraOResultado()
    {
        await using var servidor = await IniciarAsync(HealthStatus.Healthy, statusForaDaTag: HealthStatus.Unhealthy);

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, TimestampBase);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("status").GetString().Should().Be("healthy");
    }

    [Fact]
    public async Task RespostaSaudavel_NaoExpoeNomeDeCheckNemDescricaoDeInfra()
    {
        await using var servidor = await IniciarAsync(HealthStatus.Healthy, statusForaDaTag: HealthStatus.Degraded);

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, TimestampBase);

        var texto = await resposta.Content.ReadAsStringAsync();
        texto.Should().NotContain(NomeDoCheckDeAgentes);
        texto.Should().NotContain(DescricaoDoCheckDeAgentes);
        texto.Should().NotContain(NomeDoCheckForaDaTag);
        texto.Should().NotContain(DescricaoDoCheckForaDaTag);

        using var corpo = JsonDocument.Parse(texto);
        corpo.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("status", "checkedAt");
    }

    [Fact]
    public async Task RespostaNaoSaudavel_NaoExpoeNomeDeCheckNemDescricaoDeInfra()
    {
        await using var servidor = await IniciarAsync(HealthStatus.Unhealthy);

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, TimestampBase);

        var texto = await resposta.Content.ReadAsStringAsync();
        texto.Should().NotContain(NomeDoCheckDeAgentes);
        texto.Should().NotContain(DescricaoDoCheckDeAgentes);
    }

    [Fact]
    public async Task SemAssinatura_RespondeQuatrocentosEUmComSignatureInvalid()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await servidor.Cliente.GetAsync(new Uri(CaminhoDeSaude, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be("signature_invalid");
    }

    [Fact]
    public async Task CheckedAt_VemDoRelogioDoServidorNaoDoTimestampDoCliente()
    {
        await using var servidor = await IniciarAsync();
        const long TimestampDoCliente = TimestampBase - 100;

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, TimestampDoCliente);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("checkedAt").GetDateTimeOffset()
            .Should().Be(DateTimeOffset.FromUnixTimeSeconds(TimestampBase));
    }

    [Fact]
    public void AddApiServices_TagAgentsReady_CobreApenasDbESchemaSemPerderATagReady()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtSecret"] = "segredo-de-teste-com-pelo-menos-32-bytes!!",
            })
            .Build();

        using var provider = new ServiceCollection()
            .AddLogging()
            .AddApiServices(configuracao, Mock.Of<IWebHostEnvironment>(e => e.EnvironmentName == "Test"))
            .BuildServiceProvider();

        var registros = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        registros.Where(r => r.Tags.Contains(AgentEndpoints.TagAgentsReady))
            .Select(r => r.Name).Should().BeEquivalentTo("db", "schema");
        registros.Where(r => r.Tags.Contains("ready"))
            .Select(r => r.Name).Should().BeEquivalentTo("db", "schema", "stripe", "resend", "whatsapp");
    }
}
