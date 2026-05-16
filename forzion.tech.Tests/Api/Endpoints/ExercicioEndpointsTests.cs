using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using forzion.tech.Tests.Helpers;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Exercicios;
using forzion.tech.Application.UseCases.Exercicios.CriarExercicio;
using forzion.tech.Application.UseCases.Exercicios.ListarExercicios;
using forzion.tech.Application.Results;
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

public class ExercicioEndpointsTests : IClassFixture<ExercicioEndpointsTests.ExercicioWebFactory>
{
    private readonly ExercicioWebFactory _factory;
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public ExercicioEndpointsTests(ExercicioWebFactory factory)
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
    public async Task Post_Criar_DadosValidos_Retorna201()
    {
        var responseExercicio = new ExercicioResponse(Guid.NewGuid(), "Supino", GrupoMuscular.Peito, "Desc", TreinadorId, false, DateTime.UtcNow, null);
        _factory.CriarHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CriarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(responseExercicio));

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/exercicios",
            new { nome = "Supino", grupoMuscular = GrupoMuscular.Peito, descricao = "Desc" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Get_Listar_Retorna200()
    {
        var responseListar = new ListarExerciciosResponse(new List<ExercicioResponse>(), 0, 1, 20);
        _factory.ListarHandlerMock.Setup(h => h.HandleAsync(It.IsAny<ListarExerciciosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseListar);

        var response = await CriarClienteAutenticado().GetAsync("/exercicios");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    public class ExercicioWebFactory : WebApplicationFactory<Program>
    {
        private static readonly IValidator<CriarExercicioCommand> CriarValidator = 
            new CriarExercicioCommandValidator();

        public Mock<CriarExercicioHandler> CriarHandlerMock { get; } = new(
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IUnitOfWork>(),
            CriarValidator,
            Mock.Of<ILogger<CriarExercicioHandler>>())
        {
            CallBase = true
        };

        public Mock<ListarExerciciosHandler> ListarHandlerMock { get; } = new(
            Mock.Of<IExercicioRepository>(),
            Mock.Of<ILogger<ListarExerciciosHandler>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.AddForzionAITestMocks();

                services.RemoveAll<CriarExercicioHandler>();
                services.RemoveAll<ListarExerciciosHandler>();
                services.AddScoped(_ => CriarHandlerMock.Object);
                services.AddScoped(_ => ListarHandlerMock.Object);

                services.RemoveAll<IUserContext>();
                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.PerfilId).Returns(TreinadorId);
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
