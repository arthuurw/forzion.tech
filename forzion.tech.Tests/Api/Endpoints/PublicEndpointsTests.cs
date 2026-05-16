using System.Net;
using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Tests.Helpers;
using forzion.tech.Application.UseCases.Pacotes;
using forzion.tech.Application.UseCases.Pacotes.ListarPacotesAluno;
using forzion.tech.Application.UseCases.Planos;
using forzion.tech.Application.UseCases.Planos.ListarPlanosTreinador;
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
        public Mock<ListarPlanosTreinadorHandler> ListarPlanosHandlerMock { get; } = new(
            Mock.Of<IPlanoTreinadorRepository>());

        public Mock<ListarPacotesAlunoHandler> ListarPacotesHandlerMock { get; } = new(
            Mock.Of<IPacoteAlunoRepository>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.AddForzionAITestMocks();

                services.RemoveAll<ListarPlanosTreinadorHandler>();
                services.RemoveAll<ListarPacotesAlunoHandler>();

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
            .ReturnsAsync(new List<PlanoTreinadorResponse>());

        var response = await factory.CreateClient().GetAsync("/auth/planos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_AuthTreinadoresPacotes_Retorna200()
    {
        factory.ListarPacotesHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PacoteAlunoResponse>());

        var response = await factory.CreateClient().GetAsync($"/auth/treinadores/{Guid.NewGuid()}/pacotes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
