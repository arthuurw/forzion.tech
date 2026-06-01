using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

public class WebhookEndpointsTests : IClassFixture<WebhookEndpointsTests.WebhookWebFactory>
{
    private readonly WebhookWebFactory _factory;

    public WebhookEndpointsTests(WebhookWebFactory factory)
    {
        _factory = factory;
    }

    // --- POST /webhooks/stripe ---

    [Fact]
    public async Task Post_WebhookStripe_Sucesso_Retorna200()
    {
        _factory.ProcessarWebhookHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ProcessarWebhookStripeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var content = new StringContent("{\"type\":\"payment_intent.succeeded\"}", Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", "t=123,v1=abc");

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe") { Content = content };
        request.Headers.Add("Stripe-Signature", "t=123,v1=abc");

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_WebhookStripe_Invalido_Retorna400ProblemDetails()
    {
        _factory.ProcessarWebhookHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ProcessarWebhookStripeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Business("AssinaturaAluno Stripe inválida.")));

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe") { Content = content };

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // G-SEC-4: response must be ProblemDetails (application/problem+json), not a plain string.
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    // --- GET /webhooks/whatsapp (Meta verification handshake) ---

    [Fact]
    public async Task Get_WebhookWhatsApp_TokenCorreto_Retorna200ComChallenge()
    {
        // The Test environment sets WhatsApp:WebhookVerifyToken via appsettings or defaults to empty.
        // We exercise the happy-path by sending the same token the factory configures.
        var response = await _factory.CreateClient()
            .GetAsync("/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=&hub.challenge=abc123");

        // When expectedToken is empty and verifyToken is empty the constant-time compare passes.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("abc123");
    }

    [Fact]
    public async Task Get_WebhookWhatsApp_TokenErrado_Retorna403()
    {
        var response = await _factory.CreateClient()
            .GetAsync("/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=wrong-token&hub.challenge=abc123");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_WebhookWhatsApp_ModeErrado_Retorna403()
    {
        var response = await _factory.CreateClient()
            .GetAsync("/webhooks/whatsapp?hub.mode=unsubscribe&hub.verify_token=&hub.challenge=abc123");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- WebApplicationFactory ---

    public class WebhookWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ProcessarWebhookStripeHandler> ProcessarWebhookHandlerMock { get; } = new(
            Mock.Of<IPagamentoRepository>(),
            Mock.Of<IAssinaturaAlunoRepository>(),
            Mock.Of<IContaRecebimentoRepository>(),
            Mock.Of<IStripeService>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<ILogger<ProcessarWebhookStripeHandler>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ProcessarWebhookStripeHandler>();
                services.AddScoped(_ => ProcessarWebhookHandlerMock.Object);
            });
        }
    }
}
