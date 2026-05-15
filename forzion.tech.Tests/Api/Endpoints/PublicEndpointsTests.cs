using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using forzion.tech.Application.UseCases.Pacotes;
using forzion.tech.Application.UseCases.Planos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace forzion.tech.Tests.Api.Endpoints;

public class PublicEndpointsTests(PublicEndpointsTests.PublicWebFactory factory) : IClassFixture<PublicEndpointsTests.PublicWebFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    public class PublicWebFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("AllowedHosts", "*");
            // Garante secret válido em CI (User Secrets cobre o ambiente local)
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");
        }
    }

    [Fact]
    public async Task Get_AuthPlanos_Retorna200()
    {
        var response = await _client.GetAsync("/auth/planos");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_AuthTreinadoresPacotes_Retorna200()
    {
        var response = await _client.GetAsync($"/auth/treinadores/{Guid.NewGuid()}/pacotes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
