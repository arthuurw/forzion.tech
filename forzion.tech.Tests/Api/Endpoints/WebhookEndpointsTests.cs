using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
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
    public async Task Post_WebhookStripe_Invalido_Retorna400()
    {
        _factory.ProcessarWebhookHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ProcessarWebhookStripeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Business("Assinatura Stripe inválida.")));

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe") { Content = content };

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- WebApplicationFactory ---

    public class WebhookWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ProcessarWebhookStripeHandler> ProcessarWebhookHandlerMock { get; } = new(
            Mock.Of<IPagamentoRepository>(),
            Mock.Of<IAssinaturaRepository>(),
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IStripeService>(),
            Mock.Of<IUnitOfWork>(),
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
