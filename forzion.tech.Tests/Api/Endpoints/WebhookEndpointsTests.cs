using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
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
            .ReturnsAsync(Result.Failure(Error.Business("webhook_stripe.assinatura_invalida", "AssinaturaAluno Stripe inválida.")));

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe") { Content = content };

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // G-SEC-4: response must be ProblemDetails (application/problem+json), not a plain string.
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Post_WebhookStripe_SemHeaderAssinatura_RepassaAssinaturaVaziaERetorna400()
    {
        // Handler é mockado, então a leitura do header Stripe-Signature pelo endpoint
        // não era exercitada. Captura o command p/ provar o plumbing: header ausente
        // => AssinaturaAlunoStripe == "" => handler falha => 400.
        ProcessarWebhookStripeCommand? captured = null;
        _factory.ProcessarWebhookHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ProcessarWebhookStripeCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessarWebhookStripeCommand, CancellationToken>((c, _) => captured = c)
            .ReturnsAsync((ProcessarWebhookStripeCommand c, CancellationToken _) =>
                string.IsNullOrEmpty(c.AssinaturaAlunoStripe)
                    ? Result.Failure(Error.Business("webhook_stripe.assinatura_ausente", "Assinatura ausente."))
                    : Result.Success());

        var content = new StringContent("{\"type\":\"payment_intent.succeeded\"}", Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe") { Content = content };

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        captured.Should().NotBeNull();
        captured!.AssinaturaAlunoStripe.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_WebhookStripe_ComHeaderAssinatura_RepassaValorDoHeaderAoHandler()
    {
        ProcessarWebhookStripeCommand? captured = null;
        _factory.ProcessarWebhookHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ProcessarWebhookStripeCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessarWebhookStripeCommand, CancellationToken>((c, _) => captured = c)
            .ReturnsAsync(Result.Success());

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe") { Content = content };
        request.Headers.Add("Stripe-Signature", "t=1,v1=deadbeef");

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        captured.Should().NotBeNull();
        captured!.AssinaturaAlunoStripe.Should().Be("t=1,v1=deadbeef");
    }

    // --- POST /webhooks/resend / /webhooks/whatsapp ---

    [Fact]
    public async Task Post_WebhookResend_Invalido_NaoEcoaErrorMessage()
    {
        _factory.ProcessarWebhookResendHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ProcessarWebhookResendCommand>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Business("webhook_email.assinatura_invalida", "Assinatura do webhook inválida.")));

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _factory.CreateClient().PostAsync("/webhooks/resend", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.GetProperty("detail").GetString().Should().Be("Webhook inválido.");
    }

    [Fact]
    public async Task Post_WebhookWhatsApp_Invalido_NaoEcoaErrorMessage()
    {
        _factory.ProcessarWebhookWhatsAppHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ProcessarWebhookWhatsAppCommand>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Business("webhook_whatsapp.assinatura_invalida", "Assinatura inválida.")));

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _factory.CreateClient().PostAsync("/webhooks/whatsapp", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.GetProperty("detail").GetString().Should().Be("Webhook inválido.");
    }

    // --- GET /webhooks/whatsapp (Meta verification handshake) ---

    [Fact]
    public async Task Get_WebhookWhatsApp_TokenCorreto_Retorna200ComChallenge()
    {
        // Token real configurado na factory (não vazio) — exercita o match verdadeiro,
        // não o caso degenerado empty==empty.
        var response = await _factory.CreateClient()
            .GetAsync($"/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token={WebhookWebFactory.VerifyToken}&hub.challenge=abc123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("abc123");
    }

    [Fact]
    public async Task Get_WebhookWhatsApp_TokenVazio_Retorna403()
    {
        // Com token real configurado, um verify_token vazio NÃO pode passar.
        var response = await _factory.CreateClient()
            .GetAsync("/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=&hub.challenge=abc123");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        public const string VerifyToken = "meta-verify-token-test";

        public Mock<ProcessarWebhookStripeHandler> ProcessarWebhookHandlerMock { get; } = new(
            Mock.Of<IPagamentoRepository>(),
            Mock.Of<IAssinaturaAlunoRepository>(),
            Mock.Of<IContaRecebimentoRepository>(),
            Mock.Of<IPagamentoTreinadorRepository>(),
            Mock.Of<IAssinaturaTreinadorRepository>(),
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<IContaRepository>(),
            Mock.Of<IStripeService>(),
            Mock.Of<IUnitOfWork>(), Mock.Of<IOutboxEnfileirador>(),
            Mock.Of<IDatabaseErrorInspector>(), TimeProvider.System,
            Mock.Of<ILogger<ProcessarWebhookStripeHandler>>());

        public Mock<ProcessarWebhookResendHandler> ProcessarWebhookResendHandlerMock { get; } = new(
            Mock.Of<IEmailDeliveryLogRepository>(),
            Mock.Of<IUnitOfWork>(),
            TimeProvider.System,
            Mock.Of<IRecipientHasher>(),
            Mock.Of<ILogger<ProcessarWebhookResendHandler>>());

        public Mock<ProcessarWebhookWhatsAppHandler> ProcessarWebhookWhatsAppHandlerMock { get; } = new(
            Mock.Of<IWhatsAppDeliveryLogRepository>(),
            Mock.Of<IUnitOfWork>(),
            TimeProvider.System,
            Mock.Of<IRecipientHasher>(),
            Mock.Of<ILogger<ProcessarWebhookWhatsAppHandler>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");
            builder.UseSetting("WhatsApp:WebhookVerifyToken", VerifyToken);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ProcessarWebhookStripeHandler>();
                services.AddScoped(_ => ProcessarWebhookHandlerMock.Object);
                services.RemoveAll<ProcessarWebhookResendHandler>();
                services.AddScoped(_ => ProcessarWebhookResendHandlerMock.Object);
                services.RemoveAll<ProcessarWebhookWhatsAppHandler>();
                services.AddScoped(_ => ProcessarWebhookWhatsAppHandlerMock.Object);
            });
        }
    }
}
