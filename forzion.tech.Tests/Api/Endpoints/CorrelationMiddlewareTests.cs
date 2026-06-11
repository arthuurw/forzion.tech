using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace forzion.tech.Tests.Api.Endpoints;

// Unit (sem Docker): verifica o comportamento do middleware de correlation id (OBS-01).
// Usa /health como rota anônima e sem dependência de infra para isolar o middleware.
public class CorrelationMiddlewareTests(CorrelationMiddlewareTests.CorrelationWebFactory factory)
    : IClassFixture<CorrelationMiddlewareTests.CorrelationWebFactory>
{
    public class CorrelationWebFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");
        }
    }

    [Fact]
    public async Task Get_SemHeaderEntrada_RespondeComXRequestId()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        // HttpResponseHeaders.Contains é case-insensitive (nome de header HTTP); ContainKey
        // do FluentAssertions seria ordinal e falharia com a forma canônica do HttpClient.
        response.Headers.Contains("X-Request-Id").Should().BeTrue();
        response.Headers.GetValues("X-Request-Id").Should().ContainSingle()
            .Which.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Get_ComHeaderEntrada_EchoaOMesmoId()
    {
        var idEntrada = "meu-request-id-123";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Request-Id", idEntrada);

        var response = await factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        response.Headers.GetValues("X-Request-Id").Should().ContainSingle()
            .Which.Should().Be(idEntrada);
    }
}
