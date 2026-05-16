using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.RegistrarAluno;
using forzion.tech.Application.UseCases.Auth.Login;
using forzion.tech.Application.UseCases.Pacotes;
using forzion.tech.Application.UseCases.Pacotes.ListarPacotesAluno;
using forzion.tech.Application.UseCases.Planos;
using forzion.tech.Application.UseCases.Planos.ListarPlanosTreinador;
using forzion.tech.Application.UseCases.Treinadores;
using forzion.tech.Application.UseCases.Treinadores.ListarTreinadoresPublicos;
using forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

public class AuthEndpointsTests : IClassFixture<AuthEndpointsTests.AuthWebFactory>
{
    private readonly AuthWebFactory _factory;

    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();

    private static readonly TreinadorResponse RespostaTreinador = new(
        TreinadorId, Guid.NewGuid(), "Carlos", TreinadorStatus.AguardandoAprovacao, null, DateTime.UtcNow);

    private static readonly AlunoResponse RespostaAluno = new(
        AlunoId, "João Silva", "joao@test.com", null, AlunoStatus.AguardandoAprovacao,
        Guid.NewGuid(), DateTime.UtcNow, null);

    private static readonly LoginResponse RespostaLogin = new(
        "token.jwt.fake", TipoConta.Treinador, Guid.NewGuid(), TreinadorId);

    public AuthEndpointsTests(AuthWebFactory factory)
    {
        _factory = factory;
    }

    // --- POST /auth/login ---

    [Fact]
    public async Task Post_Login_CredenciaisValidas_Retorna200()
    {
        _factory.LoginHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaLogin);

        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/login",
            new { Email = "treinador@test.com", Senha = "Senha@123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("token").GetString().Should().Be("token.jwt.fake");
    }

    [Fact]
    public async Task Post_Login_CredenciaisInvalidas_Retorna401()
    {
        _factory.LoginHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CredenciaisInvalidasException());

        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/login",
            new { Email = "treinador@test.com", Senha = "errada" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Login_DadosInvalidos_Retorna400()
    {
        _factory.LoginHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[]
            {
                new ValidationFailure("Email", "Email inválido.")
            }));

        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/login",
            new { Email = "", Senha = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- POST /auth/register/treinador ---

    [Fact]
    public async Task Post_RegistrarTreinador_DadosValidos_Retorna201()
    {
        _factory.RegistrarTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<RegistrarTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaTreinador);

        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/register/treinador",
            new { Email = "novo@test.com", Senha = "Senha@123", Nome = "Carlos" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_RegistrarTreinador_DadosInvalidos_Retorna400()
    {
        _factory.RegistrarTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<RegistrarTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[]
            {
                new ValidationFailure("Email", "Email inválido.")
            }));

        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/register/treinador",
            new { Email = "", Senha = "", Nome = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- POST /auth/register/aluno ---

    [Fact]
    public async Task Post_RegistrarAluno_DadosValidos_Retorna201()
    {
        _factory.RegistrarAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<RegistrarAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaAluno));

        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/register/aluno",
            new
            {
                Email = "aluno@test.com",
                Senha = "Senha@123",
                Nome = "João",
                TreinadorId,
                PacoteId = Guid.NewGuid()
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- POST /auth/register/aluno (falha) ---

    [Fact]
    public async Task Post_RegistrarAluno_Falha_Retorna422()
    {
        _factory.RegistrarAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<RegistrarAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<AlunoResponse>(Error.Business("Treinador indisponível.")));

        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/register/aluno",
            new
            {
                Email = "aluno@test.com",
                Senha = "Senha@123",
                Nome = "João",
                TreinadorId,
                PacoteId = Guid.NewGuid()
            });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- GET /auth/treinadores ---

    [Fact]
    public async Task Get_Treinadores_Retorna200()
    {
        _factory.ListarTreinadoresPublicosHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TreinadorResponse> { RespostaTreinador });

        var response = await _factory.CreateClient().GetAsync("/auth/treinadores");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- WebApplicationFactory ---

    public class AuthWebFactory : WebApplicationFactory<Program>
    {
        public Mock<LoginHandler> LoginHandlerMock { get; } = new(
            Mock.Of<IContaRepository>(),
            Mock.Of<IJwtService>(),
            Mock.Of<IPasswordHasher>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<ISystemUserRepository>(),
            Mock.Of<IValidator<LoginCommand>>(),
            Mock.Of<ILogger<LoginHandler>>());

        public Mock<RegistrarTreinadorHandler> RegistrarTreinadorHandlerMock { get; } = new(
            Mock.Of<IContaRepository>(),
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IPasswordHasher>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<RegistrarTreinadorCommand>>(),
            Mock.Of<ILogger<RegistrarTreinadorHandler>>());

        public Mock<RegistrarAlunoHandler> RegistrarAlunoHandlerMock { get; } = new(
            Mock.Of<IContaRepository>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IPacoteAlunoRepository>(),
            Mock.Of<IPasswordHasher>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<RegistrarAlunoCommand>>(),
            Mock.Of<IWhatsAppNotifier>(),
            Mock.Of<ILogger<RegistrarAlunoHandler>>());

        public Mock<ListarTreinadoresPublicosHandler> ListarTreinadoresPublicosHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>());

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
                services.RemoveAll<LoginHandler>();
                services.RemoveAll<RegistrarTreinadorHandler>();
                services.RemoveAll<RegistrarAlunoHandler>();
                services.RemoveAll<ListarTreinadoresPublicosHandler>();
                services.RemoveAll<ListarPlanosTreinadorHandler>();
                services.RemoveAll<ListarPacotesAlunoHandler>();

                services.AddScoped(_ => LoginHandlerMock.Object);
                services.AddScoped(_ => RegistrarTreinadorHandlerMock.Object);
                services.AddScoped(_ => RegistrarAlunoHandlerMock.Object);
                services.AddScoped(_ => ListarTreinadoresPublicosHandlerMock.Object);
                services.AddScoped(_ => ListarPlanosHandlerMock.Object);
                services.AddScoped(_ => ListarPacotesHandlerMock.Object);
            });
        }
    }
}
