using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Exercicios;
using forzion.tech.Application.UseCases.Exercicios.CriarExercicio;
using forzion.tech.Application.UseCases.Exercicios.ListarExercicios;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
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

    public ExercicioEndpointsTests(ExercicioWebFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CriarClienteAutenticado()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "treinador");
        return client;
    }

    private HttpClient CriarClienteAluno()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "aluno");
        return client;
    }

    [Fact]
    public async Task Post_Criar_DadosValidos_Retorna201()
    {
        var responseExercicio = new ExercicioResponse(Guid.NewGuid(), "Supino", Guid.NewGuid(), "Peito", "Desc", null, null, TreinadorId, false, DateTime.UtcNow, null);
        _factory.CriarHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CriarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(responseExercicio));

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/exercicios",
            new { nome = "Supino", grupoMuscularId = Guid.NewGuid(), descricao = "Desc" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_Criar_ComOrientacao_RepassaCamposParaCommand()
    {
        CriarExercicioCommand? capturado = null;
        var responseExercicio = new ExercicioResponse(Guid.NewGuid(), "Supino", Guid.NewGuid(), "Peito", null,
            "Mantenha a postura.", "dQw4w9WgXcQ", TreinadorId, false, DateTime.UtcNow, null);
        _factory.CriarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CriarExercicioCommand, CancellationToken>((c, _) => capturado = c)
            .ReturnsAsync(Result.Success(responseExercicio));

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/exercicios",
            new { nome = "Supino", grupoMuscularId = Guid.NewGuid(), comoExecutar = "Mantenha a postura.", videoUrl = "https://youtu.be/dQw4w9WgXcQ" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        capturado!.ComoExecutar.Should().Be("Mantenha a postura.");
        capturado.VideoUrl.Should().Be("https://youtu.be/dQw4w9WgXcQ");
    }

    [Fact]
    public async Task Post_Criar_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/exercicios",
            new { nome = "X", grupoMuscularId = Guid.NewGuid() });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Criar_DomainException_Retorna422()
    {
        _factory.CriarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ExercicioResponse>(Error.Business("exercicio.nome_duplicado", "Nome duplicado.")));

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/exercicios",
            new { nome = "Supino", grupoMuscularId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_Criar_ValidationException_Retorna400()
    {
        _factory.CriarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarExercicioCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FluentValidation.ValidationException(
                new[] { new FluentValidation.Results.ValidationFailure("Nome", "Obrigatório") }));

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/exercicios",
            new { nome = "", grupoMuscularId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Listar_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/exercicios");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    [Fact]
    public async Task Post_Criar_AlunoRole_Retorna403()
    {
        var response = await CriarClienteAluno().PostAsJsonAsync("/exercicios",
            new { nome = "Supino", grupoMuscularId = Guid.NewGuid() });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Listar_AlunoRole_Retorna403()
    {
        var response = await CriarClienteAluno().GetAsync("/exercicios");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    public class ExercicioWebFactory : WebApplicationFactory<Program>
    {
        private static readonly IValidator<CriarExercicioCommand> CriarValidator =
            new CriarExercicioCommandValidator();

        public Mock<CriarExercicioHandler> CriarHandlerMock { get; } = new(
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IGrupoMuscularRepository>(),
            Mock.Of<IUnitOfWork>(),
            CriarValidator, TimeProvider.System,
            Mock.Of<ILogger<CriarExercicioHandler>>())
        {
            CallBase = true
        };

        public Mock<ListarExerciciosHandler> ListarHandlerMock { get; } = new(
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IGrupoMuscularRepository>(),
            Mock.Of<ILogger<ListarExerciciosHandler>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
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

            var param = header.Replace("Test ", "");

            var tipoConta = param switch
            {
                "treinador" => "Treinador",
                "aluno" => "Aluno",
                "admin" => "SystemAdmin",
                _ => null,
            };
            if (tipoConta is null)
                return Task.FromResult(AuthenticateResult.Fail("Token inválido"));

            var claims = new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("tipo_conta", tipoConta),
                new Claim("perfil_id", TreinadorId.ToString()),
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
