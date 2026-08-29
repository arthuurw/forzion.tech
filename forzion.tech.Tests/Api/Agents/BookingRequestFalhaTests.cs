using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using FluentAssertions;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Endpoints.Agents;
using forzion.tech.Api.Endpoints.Agents.Hmac;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Agents.Agendamentos;
using forzion.tech.Tests.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace forzion.tech.Tests.Api.Agents;

// AGF4-11: prova que falha de infra em QUALQUER ponto do fluxo do POST booking-requests vira 503
// dependency_unavailable — nunca 201 (disfarçado de sucesso) e nunca 404/409 (disfarçado de decisão
// de negócio). Molde: DisponibilidadeFalhaTests (fatia 3) — handler REAL registrado no DI, com um
// repositório que lança; diferente de BookingRequestEndpointTests, que mocka o handler inteiro.
public class BookingRequestFalhaTests
{
    private const string Segredo = "segredo-atual-com-pelo-menos-32-bytes!!";

    private static WebApplicationBuilder CriarBuilder()
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
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<HmacSignatureVerifier>();
        builder.Services.AddHealthChecks();
        builder.Services.AddAuthentication();
        builder.Services.AddRateLimiter(opt =>
            opt.AddPolicy("agents", _ => RateLimitPartition.GetNoLimiter<string>("test")));
        builder.Services.AddSingleton(Mock.Of<IUnitOfWork>());
        builder.Services.AddSingleton(Mock.Of<IDatabaseErrorInspector>());
        builder.Services.AddScoped<ResolvedorLeadAgendamento>();
        builder.Services.AddScoped<RegistrarSolicitacaoAgendamentoHandler>();
        return builder;
    }

    private static async Task<HttpResponseMessage> EnviarAssinadaAsync(HttpClient cliente, string caminho, string corpoJson)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var corpoBytes = Encoding.UTF8.GetBytes(corpoJson);
        var payload = $"POST\n{caminho}\n{Convert.ToHexStringLower(SHA256.HashData(corpoBytes))}\n{timestamp}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Segredo), Encoding.UTF8.GetBytes(payload));

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, caminho)
        {
            Content = new StringContent(corpoJson, Encoding.UTF8, "application/json")
        };
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeAssinatura, "v1=" + Convert.ToHexStringLower(mac));
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeTimestamp, timestamp.ToString(provider: null));

        return await cliente.SendAsync(requisicao);
    }

    private static void AssertarFalhaDeInfra(HttpResponseMessage resposta, JsonDocument corpo)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        resposta.StatusCode.Should().NotBe(HttpStatusCode.Created);
        resposta.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        resposta.StatusCode.Should().NotBe(HttpStatusCode.Conflict);
        corpo.RootElement.GetProperty("code").GetString().Should().Be("dependency_unavailable");
        corpo.RootElement.TryGetProperty("bookingRequestId", out _).Should().BeFalse("a falha não pode se disfarçar de decisão de negócio nem vazar dado de domínio");
        corpo.RootElement.GetProperty("detail").GetString().Should().NotContain("timeout de conexao com o postgres", "a mensagem interna da exceção não pode vazar no corpo da resposta");
    }

    [Fact]
    public async Task RepositorioDeTreinadorLancaNoInicioDoFluxo_Retorna503ComDependencyUnavailable()
    {
        var builder = CriarBuilder();
        var treinadorRepoQueLanca = new Mock<ITreinadorRepository>();
        treinadorRepoQueLanca
            .Setup(r => r.ObterPorIdSemTrackingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("timeout de conexao com o postgres"));
        builder.Services.AddSingleton(treinadorRepoQueLanca.Object);
        builder.Services.AddSingleton(new Mock<IPacoteRepository>().Object);
        builder.Services.AddSingleton(new Mock<IBloqueioAgendaRepository>().Object);
        builder.Services.AddSingleton(new Mock<ISolicitacaoAgendamentoRepository>().Object);
        builder.Services.AddSingleton(new Mock<ILeadRepository>().Object);

        await using var app = builder.Build();
        app.UseRateLimiter();
        app.MapAgentEndpoints();
        await app.StartAsync();
        using var cliente = app.GetTestClient();

        var tenantId = Guid.NewGuid();
        var caminho = $"{AgentEndpoints.Prefixo}/tenants/{tenantId}/booking-requests";
        using var resposta = await EnviarAssinadaAsync(cliente, caminho, CorpoValido(Guid.NewGuid()));

        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        AssertarFalhaDeInfra(resposta, corpo);
    }

    [Fact]
    public async Task RepositorioDeSolicitacaoLancaNaChecagemDeIdempotencia_Retorna503ComDependencyUnavailable()
    {
        var builder = CriarBuilder();

        var treinador = new TreinadorBuilder().Build();
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.PerfilPublico.AtualizarDados("Studio Falha", null, null, DateTime.UtcNow);
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);
        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).Build();
        pacote.AtualizarCatalogoPublico("Categoria", 60, false, DateTime.UtcNow);
        pacote.TornarPublico(DateTime.UtcNow);

        var treinadorRepo = new Mock<ITreinadorRepository>();
        treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        builder.Services.AddSingleton(treinadorRepo.Object);
        var pacoteRepo = new Mock<IPacoteRepository>();
        pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        builder.Services.AddSingleton(pacoteRepo.Object);
        builder.Services.AddSingleton(new Mock<IBloqueioAgendaRepository>().Object);
        builder.Services.AddSingleton(new Mock<ILeadRepository>().Object);

        var solicitacaoRepoQueLanca = new Mock<ISolicitacaoAgendamentoRepository>();
        solicitacaoRepoQueLanca
            .Setup(r => r.ObterPorIdempotencyKeyAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("timeout de conexao com o postgres"));
        builder.Services.AddSingleton(solicitacaoRepoQueLanca.Object);

        await using var app = builder.Build();
        app.UseRateLimiter();
        app.MapAgentEndpoints();
        await app.StartAsync();
        using var cliente = app.GetTestClient();

        var caminho = $"{AgentEndpoints.Prefixo}/tenants/{treinador.Id}/booking-requests";
        using var resposta = await EnviarAssinadaAsync(cliente, caminho, CorpoValido(pacote.Id));

        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        AssertarFalhaDeInfra(resposta, corpo);
    }

    private static string CorpoValido(Guid serviceId) =>
        $$"""{"serviceId":"{{serviceId}}","slotId":"slot-qualquer","name":"Fulano","contact":{"type":"email","value":"fulano@lead.com"},"consent":{"granted":true,"purpose":"Contato comercial"},"idempotencyKey":"chave-{{Guid.NewGuid():N}}"}""";
}
