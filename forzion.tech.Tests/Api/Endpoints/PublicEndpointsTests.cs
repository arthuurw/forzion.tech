using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using forzion.tech.Application.UseCases.Pacotes;
using forzion.tech.Application.UseCases.Planos;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace forzion.tech.Tests.Api.Endpoints;

public class PublicEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

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
