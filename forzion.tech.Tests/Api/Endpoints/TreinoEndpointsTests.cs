using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos;
using forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;
using forzion.tech.Application.UseCases.Treinos.CriarTreino;
using forzion.tech.Application.UseCases.Treinos.ListarTreinos;
using forzion.tech.Application.UseCases.Treinos.ObterTreino;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

public class TreinoEndpointsTests : IClassFixture<TreinoEndpointsTests.TreinoWebFactory>
{
    private readonly TreinoWebFactory _factory;
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public TreinoEndpointsTests(TreinoWebFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CriarClienteAutenticado()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", UserId.ToString());
        return client;
    }

    [Fact]
    public async Task Post_Criar_Retorna201()
    {
        var responseTreino = new TreinoResponse(Guid.NewGuid(), "Treino A", ObjetivoTreino.Hipertrofia, TenantId, Guid.NewGuid(), new List<TreinoExercicioResponse>(), DateTime.UtcNow, null);
        _factory.CriarHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CriarTreinoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseTreino);

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/treinos",
            new { alunoId = Guid.NewGuid(), nome = "Treino A", objetivo = ObjetivoTreino.Hipertrofia });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Get_Obter_Retorna200()
    {
        var responseTreino = new TreinoResponse(Guid.NewGuid(), "Treino A", ObjetivoTreino.Hipertrofia, TenantId, Guid.NewGuid(), new List<TreinoExercicioResponse>(), DateTime.UtcNow, null);
        _factory.ObterHandlerMock.Setup(h => h.HandleAsync(It.IsAny<ObterTreinoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseTreino);

        var response = await CriarClienteAutenticado().GetAsync($"/treinos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_AdicionarExercicio_Retorna200()
    {
        var responseTreino = new TreinoResponse(Guid.NewGuid(), "Treino A", ObjetivoTreino.Hipertrofia, TenantId, Guid.NewGuid(), new List<TreinoExercicioResponse>(), DateTime.UtcNow, null);
        _factory.AdicionarHandlerMock.Setup(h => h.HandleAsync(It.IsAny<AdicionarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseTreino);

        var response = await CriarClienteAutenticado().PostAsJsonAsync($"/treinos/{Guid.NewGuid()}/exercicios",
            new { exercicioId = Guid.NewGuid(), series = 3, repeticoes = 10 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    public class TreinoWebFactory : WebApplicationFactory<Program>
    {
        private static readonly IValidator<CriarTreinoCommand> CriarValidator = 
            new CriarTreinoCommandValidator();

        private static readonly IValidator<AdicionarExercicioCommand> AdicionarValidator = 
            new AdicionarExercicioCommandValidator();

        public Mock<CriarTreinoHandler> CriarHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<IUnitOfWork>(),
            CriarValidator,
            Mock.Of<ILogger<CriarTreinoHandler>>())
        {
            CallBase = true
        };

        public Mock<ObterTreinoHandler> ObterHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<ILogger<ObterTreinoHandler>>());

        public Mock<AdicionarExercicioHandler> AdicionarHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IExecucaoTreinoRepository>(),
            Mock.Of<IUnitOfWork>(),
            AdicionarValidator,
            Mock.Of<ILogger<AdicionarExercicioHandler>>())
        {
            CallBase = true
        };

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<CriarTreinoHandler>();
                services.RemoveAll<ObterTreinoHandler>();
                services.RemoveAll<AdicionarExercicioHandler>();
                
                services.AddScoped(_ => CriarHandlerMock.Object);
                services.AddScoped(_ => ObterHandlerMock.Object);
                services.AddScoped(_ => AdicionarHandlerMock.Object);

                services.RemoveAll<IUserContext>();
                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.PerfilId).Returns(TenantId);
                services.AddScoped(_ => userContextMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }
    }

    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(header))
                return Task.FromResult(AuthenticateResult.Fail("Sem token"));

            var userId = header.Replace("Test ", "");
            var claims = new[] { new Claim("sub", userId) };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
