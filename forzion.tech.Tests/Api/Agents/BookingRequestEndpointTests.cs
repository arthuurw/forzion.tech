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
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
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

public class BookingRequestEndpointTests
{
    private const string Segredo = "segredo-atual-com-pelo-menos-32-bytes!!";
    private const string TenantId = "7f3a1b2c-9d4e-4f5a-8b6c-1d2e3f4a5b6c";
    private const string CaminhoDeBookingRequests = AgentEndpoints.Prefixo + "/tenants/" + TenantId + "/booking-requests";

    private const string CorpoValido =
        """{"serviceId":"8f3a1b2c-9d4e-4f5a-8b6c-1d2e3f4a5b6d","slotId":"abc123","name":"Fulano","contact":{"type":"email","value":"fulano@lead.com"},"consent":{"granted":true,"purpose":"Contato comercial"},"idempotencyKey":"chave-1"}""";

    private sealed record Servidor(WebApplication App, HttpClient Cliente) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Cliente.Dispose();
            await App.DisposeAsync();
        }
    }

    private static async Task<Servidor> IniciarAsync(Mock<RegistrarSolicitacaoAgendamentoHandler> mockHandler)
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
        builder.Services.AddSingleton(mockHandler.Object);

        var app = builder.Build();
        app.UseRateLimiter();
        app.MapAgentEndpoints();

        await app.StartAsync();
        return new Servidor(app, app.GetTestClient());
    }

    private static Mock<RegistrarSolicitacaoAgendamentoHandler> CriarMockHandler(Result<StagedBookingRequest> resultado)
    {
        var mock = new Mock<RegistrarSolicitacaoAgendamentoHandler>(
            Mock.Of<ITreinadorRepository>(), Mock.Of<IPacoteRepository>(), Mock.Of<IBloqueioAgendaRepository>(),
            Mock.Of<ISolicitacaoAgendamentoRepository>(), new ResolvedorLeadAgendamento(Mock.Of<ILeadRepository>()),
            Mock.Of<IUnitOfWork>(), TimeProvider.System, Mock.Of<IDatabaseErrorInspector>());
        mock.Setup(h => h.HandleAsync(It.IsAny<RegistrarSolicitacaoAgendamentoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultado);
        return mock;
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

    private static async Task<string> LerCodeAsync(HttpResponseMessage resposta)
    {
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return documento.RootElement.GetProperty("code").GetString()!;
    }

    // --- 201 ---

    [Fact]
    public async Task DadosValidos_Retorna201ComBookingRequestIdEStatusPendingAgent()
    {
        var bookingRequestId = Guid.NewGuid().ToString();
        var mockHandler = CriarMockHandler(Result.Success(new StagedBookingRequest(bookingRequestId, "pending-agent")));
        await using var servidor = await IniciarAsync(mockHandler);

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, CaminhoDeBookingRequests, CorpoValido);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("bookingRequestId").GetString().Should().Be(bookingRequestId);
        corpo.RootElement.GetProperty("status").GetString().Should().Be("pending-agent");
    }

    // --- 400 ---

    [Fact]
    public async Task TenantIdNaoGuid_Retorna400ComValidationFailedSemChamarHandler()
    {
        var mockHandler = CriarMockHandler(Result.Success(new StagedBookingRequest(Guid.NewGuid().ToString(), "pending-agent")));
        await using var servidor = await IniciarAsync(mockHandler);
        var caminhoInvalido = AgentEndpoints.Prefixo + "/tenants/nao-e-guid/booking-requests";

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, caminhoInvalido, CorpoValido);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LerCodeAsync(resposta)).Should().Be("validation_failed");
        mockHandler.Verify(h => h.HandleAsync(It.IsAny<RegistrarSolicitacaoAgendamentoCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ServiceIdNaoGuid_Retorna400ComValidationFailedSemChamarHandler()
    {
        var mockHandler = CriarMockHandler(Result.Success(new StagedBookingRequest(Guid.NewGuid().ToString(), "pending-agent")));
        await using var servidor = await IniciarAsync(mockHandler);
        const string corpoServiceIdInvalido =
            """{"serviceId":"nao-e-guid","slotId":"abc123","name":"Fulano","contact":{"type":"email","value":"fulano@lead.com"},"consent":{"granted":true,"purpose":"Contato comercial"},"idempotencyKey":"chave-1"}""";

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, CaminhoDeBookingRequests, corpoServiceIdInvalido);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LerCodeAsync(resposta)).Should().Be("validation_failed");
        mockHandler.Verify(h => h.HandleAsync(It.IsAny<RegistrarSolicitacaoAgendamentoCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsentimentoNegado_Retorna400ComValidationFailed()
    {
        var mockHandler = CriarMockHandler(Result.Failure<StagedBookingRequest>(SolicitacaoAgendamentoAgenteErrors.ConsentimentoNaoConcedido));
        await using var servidor = await IniciarAsync(mockHandler);

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, CaminhoDeBookingRequests, CorpoValido);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LerCodeAsync(resposta)).Should().Be("validation_failed");
    }

    // --- 404 ---

    [Fact]
    public async Task TenantNaoEncontrado_Retorna404ComTenantNotFound()
    {
        var mockHandler = CriarMockHandler(Result.Failure<StagedBookingRequest>(TreinadorErrors.NaoEncontrado));
        await using var servidor = await IniciarAsync(mockHandler);

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, CaminhoDeBookingRequests, CorpoValido);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LerCodeAsync(resposta)).Should().Be("tenant_not_found");
    }

    [Fact]
    public async Task ServicoNaoEncontrado_Retorna404ComServiceNotFound()
    {
        var mockHandler = CriarMockHandler(Result.Failure<StagedBookingRequest>(PacoteErrors.NaoEncontrado));
        await using var servidor = await IniciarAsync(mockHandler);

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, CaminhoDeBookingRequests, CorpoValido);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LerCodeAsync(resposta)).Should().Be("service_not_found");
    }

    [Fact]
    public async Task SlotNaoEncontrado_Retorna404ComSlotNotFound()
    {
        var mockHandler = CriarMockHandler(Result.Failure<StagedBookingRequest>(SolicitacaoAgendamentoAgenteErrors.SlotNaoEncontrado));
        await using var servidor = await IniciarAsync(mockHandler);

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, CaminhoDeBookingRequests, CorpoValido);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LerCodeAsync(resposta)).Should().Be("slot_not_found");
    }

    // --- 409 ---

    [Fact]
    public async Task SlotIndisponivel_Retorna409ComSlotUnavailable()
    {
        var mockHandler = CriarMockHandler(Result.Failure<StagedBookingRequest>(SolicitacaoAgendamentoAgenteErrors.SlotIndisponivel));
        await using var servidor = await IniciarAsync(mockHandler);

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, CaminhoDeBookingRequests, CorpoValido);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LerCodeAsync(resposta)).Should().Be("slot_unavailable");
    }

    [Fact]
    public async Task IdempotencyKeyComArgumentosDiferentes_Retorna409ComIdempotencyConflict()
    {
        var mockHandler = CriarMockHandler(Result.Failure<StagedBookingRequest>(SolicitacaoAgendamentoAgenteErrors.IdempotencyConflito));
        await using var servidor = await IniciarAsync(mockHandler);

        using var resposta = await EnviarAssinadaAsync(servidor.Cliente, CaminhoDeBookingRequests, CorpoValido);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LerCodeAsync(resposta)).Should().Be("idempotency_conflict");
    }
}
