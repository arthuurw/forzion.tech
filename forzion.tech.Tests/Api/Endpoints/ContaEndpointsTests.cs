using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.AlterarSenha;
using forzion.tech.Application.UseCases.Conta.AtualizarPerfil;
using forzion.tech.Application.UseCases.Conta.Logout;
using forzion.tech.Application.UseCases.Conta.ObterPerfil;
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

public class ContaEndpointsTests : IClassFixture<ContaEndpointsTests.ContaWebFactory>
{
    private readonly ContaWebFactory _factory;

    private static readonly Guid ContaId = Guid.NewGuid();
    private static readonly Guid PerfilId = Guid.NewGuid();

    private static readonly PerfilResponse RespostaPerfil =
        new("Carlos Treinador", "carlos@test.com", "Treinador");

    public ContaEndpointsTests(ContaWebFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CriarClienteAutenticado()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", ContaId.ToString());
        return client;
    }

    // --- GET /conta/perfil ---

    [Fact]
    public async Task Get_Perfil_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/conta/perfil");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Perfil_Autenticado_Retorna200()
    {
        _factory.ObterPerfilHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaPerfil);

        var response = await CriarClienteAutenticado().GetAsync("/conta/perfil");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- PATCH /conta/perfil ---

    [Fact]
    public async Task Patch_Perfil_Autenticado_Retorna204()
    {
        _factory.AtualizarPerfilHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarPerfilCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await CriarClienteAutenticado().PatchAsJsonAsync("/conta/perfil",
            new { Nome = "Carlos Atualizado" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // --- POST /conta/senha ---

    [Fact]
    public async Task Post_AlterarSenha_Autenticado_Retorna204()
    {
        _factory.AlterarSenhaHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AlterarSenhaCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/conta/senha",
            new { SenhaAtual = "SenhaAtual@123", NovaSenha = "SenhaNova@456" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // --- POST /conta/logout ---

    [Fact]
    public async Task Post_Logout_Autenticado_Retorna204()
    {
        _factory.LogoutHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/conta/logout", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // --- WebApplicationFactory ---

    public class ContaWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ObterPerfilHandler> ObterPerfilHandlerMock { get; } = new(
            Mock.Of<IUserContext>(),
            Mock.Of<IContaRepository>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<ISystemUserRepository>());

        public Mock<AtualizarPerfilHandler> AtualizarPerfilHandlerMock { get; } = new(
            Mock.Of<IUserContext>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<ISystemUserRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<AtualizarPerfilCommand>>());

        public Mock<AlterarSenhaHandler> AlterarSenhaHandlerMock { get; } = new(
            Mock.Of<IUserContext>(),
            Mock.Of<IContaRepository>(),
            Mock.Of<IPasswordHasher>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<AlterarSenhaCommand>>());

        public Mock<LogoutHandler> LogoutHandlerMock { get; } = new(
            Mock.Of<ITokenRevogadoRepository>(),
            Mock.Of<IUserContext>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ILogger<LogoutHandler>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ObterPerfilHandler>();
                services.RemoveAll<AtualizarPerfilHandler>();
                services.RemoveAll<AlterarSenhaHandler>();
                services.RemoveAll<LogoutHandler>();
                services.RemoveAll<IUserContext>();

                services.AddScoped(_ => ObterPerfilHandlerMock.Object);
                services.AddScoped(_ => AtualizarPerfilHandlerMock.Object);
                services.AddScoped(_ => AlterarSenhaHandlerMock.Object);
                services.AddScoped(_ => LogoutHandlerMock.Object);

                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.ContaId).Returns(ContaId);
                userContextMock.Setup(u => u.PerfilId).Returns(PerfilId);
                userContextMock.Setup(u => u.TipoConta).Returns(TipoConta.Treinador);
                userContextMock.Setup(u => u.Jti).Returns(Guid.NewGuid());
                userContextMock.Setup(u => u.TokenExpiraEm).Returns(DateTime.UtcNow.AddHours(1));
                services.AddScoped(_ => userContextMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, ContaTestAuthHandler>("Test", _ => { });
            });
        }
    }

    public class ContaTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public ContaTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(header))
                return Task.FromResult(AuthenticateResult.Fail("Sem token"));

            var claims = new[]
            {
                new Claim("sub", ContaId.ToString()),
                new Claim("tipo_conta", "Treinador"),
                new Claim("perfil_id", PerfilId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
