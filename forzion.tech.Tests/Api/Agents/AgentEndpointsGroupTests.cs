using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using FluentAssertions;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Endpoints.Agents;
using forzion.tech.Api.Endpoints.Agents.Hmac;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Agents.Disponibilidade;
using forzion.tech.Domain.Entities;
using forzion.tech.Tests.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
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
        { "POST", AgentEndpoints.Prefixo + "/tenants/7f3a1b2c-9d4e-4f5a-8b6c-1d2e3f4a5b6c/booking-requests" },
    };

    private const string TenantIdValido = "7f3a1b2c-9d4e-4f5a-8b6c-1d2e3f4a5b6c";
    private const string ServiceIdValido = "8f3a1b2c-9d4e-4f5a-8b6c-1d2e3f4a5b6d";

    private static string CaminhoAvailability(string tenantId, string query) =>
        $"{AgentEndpoints.Prefixo}/tenants/{tenantId}/availability?{query}";

    public static TheoryData<string, string> CasosDeQuatrocentosDeAvailability()
    {
        var dados = new TheoryData<string, string>();
        dados.Add("tenantId nao-guid", $"{AgentEndpoints.Prefixo}/tenants/nao-e-guid/availability?serviceId={ServiceIdValido}&from=2026-08-24T00:00:00Z&to=2026-08-25T00:00:00Z");
        dados.Add("serviceId nao-guid", CaminhoAvailability(TenantIdValido, "serviceId=nao-e-guid&from=2026-08-24T00:00:00Z&to=2026-08-25T00:00:00Z"));
        dados.Add("serviceId ausente", CaminhoAvailability(TenantIdValido, "from=2026-08-24T00:00:00Z&to=2026-08-25T00:00:00Z"));
        dados.Add("from ausente", CaminhoAvailability(TenantIdValido, $"serviceId={ServiceIdValido}&to=2026-08-25T00:00:00Z"));
        dados.Add("to ausente", CaminhoAvailability(TenantIdValido, $"serviceId={ServiceIdValido}&from=2026-08-24T00:00:00Z"));
        dados.Add("from malformado", CaminhoAvailability(TenantIdValido, $"serviceId={ServiceIdValido}&from=nao-e-data&to=2026-08-25T00:00:00Z"));
        dados.Add("to malformado", CaminhoAvailability(TenantIdValido, $"serviceId={ServiceIdValido}&from=2026-08-24T00:00:00Z&to=nao-e-data"));
        dados.Add("to menor ou igual a from", CaminhoAvailability(TenantIdValido, $"serviceId={ServiceIdValido}&from=2026-08-24T00:00:00Z&to=2026-08-24T00:00:00Z"));
        dados.Add("intervalo maior que 31 dias", CaminhoAvailability(TenantIdValido, $"serviceId={ServiceIdValido}&from=2026-01-01T00:00:00Z&to=2026-02-05T00:00:00Z"));
        return dados;
    }

    private static Mock<ITreinadorRepository> TreinadorRepoRetornando(Guid tenantId, Treinador? treinador)
    {
        var mock = new Mock<ITreinadorRepository>();
        mock.Setup(r => r.ObterPorIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        return mock;
    }

    private static Mock<IPacoteRepository> PacoteRepoRetornando(Guid serviceId, Pacote? pacote)
    {
        var mock = new Mock<IPacoteRepository>();
        mock.Setup(r => r.ObterPorIdAsync(serviceId, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        return mock;
    }

    private static Treinador CriarTreinadorPublicado()
    {
        var treinador = new TreinadorBuilder().Build();
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.PerfilPublico.AtualizarDados("Studio Teste", null, null, DateTime.UtcNow);
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);
        return treinador;
    }

    private sealed record Servidor(WebApplication App, HttpClient Cliente) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Cliente.Dispose();
            await App.DisposeAsync();
        }
    }

    private static async Task<Servidor> IniciarAsync(
        string ambiente = "Test", string? segredo = Segredo, Action<IServiceCollection>? configurarServicos = null)
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

        // Registro mínimo p/ os parâmetros [FromServices] dos endpoints reais do grupo serem
        // resolvíveis: a resolução de FromServices acontece no binding, antes do corpo do
        // delegate rodar — sem isto, qualquer chamada a uma rota real cairia no
        // AgentExceptionFilter como 503 em vez de exercitar a validação/handler de fato.
        builder.Services.AddSingleton(new Mock<ITreinadorRepository>().Object);
        builder.Services.AddSingleton(new Mock<IPacoteRepository>().Object);
        builder.Services.AddSingleton(new Mock<IBloqueioAgendaRepository>().Object);
        builder.Services.AddScoped<ConsultarDisponibilidadeAgenteHandler>();
        configurarServicos?.Invoke(builder.Services);

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

    [Fact]
    public async Task Availability_SemAssinatura_RespondeQuatrocentosEUm()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await servidor.Cliente.GetAsync(new Uri(
            CaminhoAvailability(TenantIdValido, $"serviceId={ServiceIdValido}&from=2026-08-24T00:00:00Z&to=2026-08-25T00:00:00Z"),
            UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("signature_invalid");
    }

    [Theory]
    [MemberData(nameof(CasosDeQuatrocentosDeAvailability))]
    public async Task Availability_EntradaInvalida_RespondeQuatrocentosComValidationFailed(string _, string caminho)
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, HttpMethod.Get, caminho);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LerCodeAsync(resposta)).Should().Be("validation_failed");
    }

    [Fact]
    public async Task Availability_TenantNaoEncontrado_Retorna404TenantNotFound()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, HttpMethod.Get,
            CaminhoAvailability(Guid.NewGuid().ToString(), $"serviceId={Guid.NewGuid()}&from=2026-08-24T00:00:00Z&to=2026-08-25T00:00:00Z"));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LerCodeAsync(resposta)).Should().Be("tenant_not_found");
    }

    [Fact]
    public async Task Availability_ServicoNaoEncontrado_Retorna404ServiceNotFoundDistintoDeTenantNotFound()
    {
        var treinador = CriarTreinadorPublicado();

        await using var servidor = await IniciarAsync(configurarServicos: services =>
            services.AddSingleton(TreinadorRepoRetornando(treinador.Id, treinador).Object));

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, HttpMethod.Get,
            CaminhoAvailability(treinador.Id.ToString(), $"serviceId={Guid.NewGuid()}&from=2026-08-24T00:00:00Z&to=2026-08-25T00:00:00Z"));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LerCodeAsync(resposta)).Should().Be("service_not_found");
    }

    [Fact]
    public async Task Availability_SemVagaNoIntervalo_Retorna200ComListaVazia()
    {
        var treinador = CriarTreinadorPublicado();
        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).Build();
        pacote.AtualizarCatalogoPublico("Categoria", 60, false, DateTime.UtcNow);
        pacote.TornarPublico(DateTime.UtcNow);

        await using var servidor = await IniciarAsync(configurarServicos: services =>
        {
            services.AddSingleton(TreinadorRepoRetornando(treinador.Id, treinador).Object);
            services.AddSingleton(PacoteRepoRetornando(pacote.Id, pacote).Object);
        });

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, HttpMethod.Get,
            CaminhoAvailability(treinador.Id.ToString(), $"serviceId={pacote.Id}&from=2026-08-24T00:00:00Z&to=2026-08-25T00:00:00Z"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetArrayLength().Should().Be(0);
    }
}
