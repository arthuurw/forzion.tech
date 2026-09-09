using System.Net;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Endpoints.Agents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using forzion.tech.Api.Endpoints.Agents.Hmac;
using System.Threading.RateLimiting;
using FluentAssertions;
using Xunit;

namespace forzion.tech.Tests.Api.Agents;

public class AgentsRouteHostFilteringTests
{
    private const string CaminhoDeSaude = AgentEndpoints.Prefixo + "/health";
    private const string AllowedHostsDoHomolog = "localhost;127.0.0.1;homolog.forzion.tech;homologacao.forzion.tech;backend";

    private static async Task<(WebApplication App, HttpClient Cliente)> IniciarComAllowedHostsAsync(string allowedHosts)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = allowedHosts,
            ["Agents:Hmac:SecretAtual"] = "segredo-atual-com-pelo-menos-32-bytes!!",
        });
        builder.Services.AddAgentsHmac(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<HmacSignatureVerifier>();
        builder.Services.AddRateLimiter(opt =>
            opt.AddPolicy("agents", _ => RateLimitPartition.GetNoLimiter<string>("test")));
        builder.Services.AddHealthChecks()
            .AddCheck(
                "check-de-agentes",
                () => new HealthCheckResult(HealthStatus.Healthy),
                tags: ["ready", AgentEndpoints.TagAgentsReady]);

        var app = builder.Build();
        app.UseRateLimiter();
        app.MapAgentEndpoints();

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    // Reproduz o incidente de 2026-09-06: o probe do deploy conecta pelo IP literal do tailnet,
    // sem `--resolve`/`-H Host`, então o Host vira o próprio IP. `AllowedHosts` do Homolog não
    // contém IP nenhum — o HostFilteringMiddleware do ASP.NET Core rejeita ANTES do grupo de
    // agentes, então nem o filtro HMAC chega a rodar. É o mesmo mecanismo do achado registrado
    // em specification-infrastructure §AllowedHosts, agora contra um probe novo, não um domínio.
    [Fact]
    public async Task HostDoProbeNaoListado_RejeitaQuatrocentosAntesDoFiltroHmac()
    {
        var (app, cliente) = await IniciarComAllowedHostsAsync(AllowedHostsDoHomolog);
        await using var _ = app;
        using var cliente2 = cliente;

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, CaminhoDeSaude);
        requisicao.Headers.Host = "100.114.212.86:8443";

        using var resposta = await cliente2.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Contraprova: usar um host JÁ presente em `AllowedHosts` (o mesmo usado pelo compose para
    // alcançar o container, "backend") libera a requisição para o filtro HMAC, que aí sim
    // recusa por falta de assinatura — é o 401 que o probe do deploy espera ver.
    [Fact]
    public async Task HostPermitidoSemAssinatura_ChegaAoFiltroHmacERecusaComQuatrocentosEUm()
    {
        var (app, cliente) = await IniciarComAllowedHostsAsync(AllowedHostsDoHomolog);
        await using var _ = app;
        using var cliente2 = cliente;

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, CaminhoDeSaude);
        requisicao.Headers.Host = "backend";

        using var resposta = await cliente2.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
