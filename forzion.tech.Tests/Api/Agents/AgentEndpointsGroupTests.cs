using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using FluentAssertions;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Endpoints.Agents;
using forzion.tech.Api.Endpoints.Agents.Hmac;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace forzion.tech.Tests.Api.Agents;

public class AgentEndpointsGroupTests
{
    private const string Segredo = "segredo-atual-com-pelo-menos-32-bytes!!";
    private const string CaminhoDeEco = AgentEndpoints.Prefixo + "/eco";
    private const string CaminhoQueLancaExcecao = AgentEndpoints.Prefixo + "/lanca-excecao";
    private const string CaminhoForaDoGrupo = "/fora-do-grupo";

    public static TheoryData<string, string> CaminhosDoContratoAindaNaoImplementados() => new()
    {
        { "GET", AgentEndpoints.Prefixo + "/tenants/7f3a1b2c-9d4e-4f5a-8b6c-1d2e3f4a5b6c/business-info" },
        { "GET", AgentEndpoints.Prefixo + "/tenants/7f3a1b2c-9d4e-4f5a-8b6c-1d2e3f4a5b6c/services" },
        { "GET", AgentEndpoints.Prefixo + "/tenants/7f3a1b2c-9d4e-4f5a-8b6c-1d2e3f4a5b6c/availability" },
        { "POST", AgentEndpoints.Prefixo + "/tenants/7f3a1b2c-9d4e-4f5a-8b6c-1d2e3f4a5b6c/leads" },
        { "POST", AgentEndpoints.Prefixo + "/tenants/7f3a1b2c-9d4e-4f5a-8b6c-1d2e3f4a5b6c/booking-requests" },
    };

    private sealed record Servidor(WebApplication App, HttpClient Cliente) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Cliente.Dispose();
            await App.DisposeAsync();
        }
    }

    private static async Task<Servidor> IniciarAsync(string ambiente = "Test", string? segredo = Segredo)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = ambiente });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "*",
            ["Agents:Hmac:SecretAtual"] = segredo,
        });
        builder.Services.AddAgentsHmac(builder.Configuration, builder.Environment);
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<HmacSignatureVerifier>();
        builder.Services.AddHealthChecks();
        builder.Services.AddOpenApiDocumentation();
        builder.Services.AddAuthentication();
        builder.Services.AddRateLimiter(opt =>
            opt.AddPolicy("agents", _ => RateLimitPartition.GetNoLimiter<string>("test")));

        var app = builder.Build();
        app.UseRateLimiter();
        app.MapOpenApi();
        app.MapAgentEndpoints();
        AgentEndpoints.CriarGrupo(app).MapGet("/eco", () => Results.Ok());
        AgentEndpoints.CriarGrupo(app).MapGet("/lanca-excecao", IResult () =>
            throw new InvalidOperationException("timeout de conexao com o postgres"));
        app.MapGet(CaminhoForaDoGrupo, () => Results.Ok());

        await app.StartAsync();
        return new Servidor(app, app.GetTestClient());
    }

    private static async Task<HttpResponseMessage> EnviarAssinadaAsync(HttpClient cliente, HttpMethod metodo, string caminho)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"{metodo.Method}\n{caminho}\n{Convert.ToHexStringLower(SHA256.HashData([]))}\n{timestamp}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Segredo), Encoding.UTF8.GetBytes(payload));

        using var requisicao = new HttpRequestMessage(metodo, caminho);
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeAssinatura, "v1=" + Convert.ToHexStringLower(mac));
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeTimestamp, timestamp.ToString(provider: null));

        return await cliente.SendAsync(requisicao);
    }

    private static async Task<string> LerCodeAsync(HttpResponseMessage resposta)
    {
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return documento.RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task EndpointSemAnotacaoPropriaNoGrupo_SemAssinatura_RespondeQuatrocentosEUm()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await servidor.Cliente.GetAsync(new Uri(CaminhoDeEco, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("signature_invalid");
    }

    [Fact]
    public async Task EndpointSemAnotacaoPropriaNoGrupo_ComAssinaturaValida_ChegaAoHandler()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, HttpMethod.Get, CaminhoDeEco);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EndpointForaDoGrupo_SemAssinatura_NaoEhAfetadoPelaVerificacao()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await servidor.Cliente.GetAsync(new Uri(CaminhoForaDoGrupo, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(CaminhosDoContratoAindaNaoImplementados))]
    public async Task CaminhoDoContratoAindaNaoImplementado_MesmoAssinado_RespondeQuatrocentosEQuatro(string metodo, string caminho)
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, new HttpMethod(metodo), caminho);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EndpointDoGrupoLancaExcecao_RespondeQuinhentosETresComDependencyUnavailable()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, HttpMethod.Get, CaminhoQueLancaExcecao);

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await LerCodeAsync(resposta)).Should().Be("dependency_unavailable");
    }

    [Fact]
    public async Task GrupoDeAgentes_NaoApareceNoDocumentoOpenApiGerado()
    {
        await using var servidor = await IniciarAsync();

        var documento = await servidor.Cliente.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        using var json = JsonDocument.Parse(documento);
        var caminhos = json.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        caminhos.Should().Contain(CaminhoForaDoGrupo);
        caminhos.Should().NotContain(c => c.StartsWith(AgentEndpoints.Prefixo, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Development_SemSegredoConfigurado_GrupoRespondeQuatrocentosEUmEmVezDeAbrir()
    {
        await using var servidor = await IniciarAsync(ambiente: "Development", segredo: null);

        using var resposta = await servidor.Cliente.GetAsync(new Uri(CaminhoDeEco, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("signature_invalid");
    }
}
