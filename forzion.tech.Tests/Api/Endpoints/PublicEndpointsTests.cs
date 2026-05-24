using System.Net;
using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pacotes;
using forzion.tech.Application.UseCases.Pacotes.ListarPacotes;
using forzion.tech.Application.UseCases.Planos;
using forzion.tech.Application.UseCases.Planos.ListarPlanosPlataforma;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace forzion.tech.Tests.Api.Endpoints;

public class PublicEndpointsTests(PublicEndpointsTests.PublicWebFactory factory) : IClassFixture<PublicEndpointsTests.PublicWebFactory>
{
    public class PublicWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ListarPlanosPlataformaHandler> ListarPlanosHandlerMock { get; } = new(
            Mock.Of<IPlanoPlataformaRepository>());

        public Mock<ListarPacotesHandler> ListarPacotesHandlerMock { get; } = new(
            Mock.Of<IPacoteRepository>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ListarPlanosPlataformaHandler>();
                services.RemoveAll<ListarPacotesHandler>();

                services.AddSingleton(ListarPlanosHandlerMock.Object);
                services.AddSingleton(ListarPacotesHandlerMock.Object);
            });
        }
    }

    [Fact]
    public async Task Get_AuthPlanos_Retorna200()
    {
        factory.ListarPlanosHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlanoPlataformaResponse>());

        var response = await factory.CreateClient().GetAsync("/auth/planos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_AuthTreinadoresPacotes_Retorna200()
    {
        factory.ListarPacotesHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PacoteResponse>());

        var response = await factory.CreateClient().GetAsync($"/auth/treinadores/{Guid.NewGuid()}/pacotes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
