using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Usuarios.AlterarStatusUsuario;
using forzion.tech.Application.UseCases.Usuarios.AtualizarUsuario;
using forzion.tech.Application.UseCases.Usuarios.ObterUsuarioAtual;
using forzion.tech.Application.UseCases.Usuarios.RegistrarUsuario;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace forzion.tech.Tests.Api.Endpoints;

public class UsuarioEndpointsTests : IClassFixture<UsuarioEndpointsTests.ForzionWebFactory>
{
    private readonly ForzionWebFactory _factory;
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private static readonly ObterUsuarioAtualResponse RespostaUsuario = new(
        UsuarioId, "João", "joao@example.com", Role.Admin, UsuarioStatus.Ativo,
        Guid.NewGuid(), "Academia", null, null, DateTime.UtcNow, null);

    public UsuarioEndpointsTests(ForzionWebFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CriarClienteAutenticado(Guid? id = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", (id ?? UsuarioId).ToString());
        return client;
    }

    // --- GET /usuarios/me ---

    [Fact]
    public async Task Get_Me_SemAutenticacao_Retorna401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/usuarios/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Me_UsuarioEncontrado_Retorna200()
    {
        _factory.ObterHandlerMock.Setup(h => h.HandleAsync(It.IsAny<ObterUsuarioAtualQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaUsuario);

        var response = await CriarClienteAutenticado().GetAsync("/usuarios/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("usuarioId").GetGuid().Should().Be(UsuarioId);
    }

    [Fact]
    public async Task Get_Me_UsuarioNaoEncontrado_Retorna404()
    {
        _factory.ObterHandlerMock.Setup(h => h.HandleAsync(It.IsAny<ObterUsuarioAtualQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UsuarioNaoEncontradoException());

        var response = await CriarClienteAutenticado().GetAsync("/usuarios/me");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Me_UsuarioInativo_Retorna403()
    {
        _factory.ObterHandlerMock.Setup(h => h.HandleAsync(It.IsAny<ObterUsuarioAtualQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UsuarioInativoException());

        var response = await CriarClienteAutenticado().GetAsync("/usuarios/me");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- POST /usuarios/registrar ---

    [Fact]
    public async Task Post_Registrar_SemAutenticacao_Retorna401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/usuarios/registrar",
            new { nome = "João", email = "j@e.com", tenantNome = "Academia" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Registrar_DadosValidos_Retorna201()
    {
        var resposta = new RegistrarUsuarioResponse(UsuarioId, "João", "j@e.com", Role.Admin, Guid.NewGuid(), "Academia");
        _factory.RegistrarHandlerMock.Setup(h => h.HandleAsync(It.IsAny<RegistrarUsuarioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resposta);

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/usuarios/registrar",
            new { nome = "João", email = "j@e.com", tenantNome = "Academia" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Theory]
    [InlineData("", "j@e.com", "Academia")]
    [InlineData("João", "invalido", "Academia")]
    [InlineData("João", "j@e.com", "")]
    public async Task Post_Registrar_DadosInvalidos_Retorna400(string nome, string email, string tenantNome)
    {
        var response = await CriarClienteAutenticado().PostAsJsonAsync("/usuarios/registrar",
            new { nome, email, tenantNome });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Registrar_NomeMuitoLongo_Retorna400()
    {
        var response = await CriarClienteAutenticado().PostAsJsonAsync("/usuarios/registrar",
            new { nome = new string('a', 101), email = "j@e.com", tenantNome = "Academia" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Registrar_UsuarioJaRegistrado_Retorna409()
    {
        _factory.RegistrarHandlerMock.Setup(h => h.HandleAsync(It.IsAny<RegistrarUsuarioCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UsuarioJaRegistradoException());

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/usuarios/registrar",
            new { nome = "João", email = "j@e.com", tenantNome = "Academia" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // --- PATCH /usuarios/me ---

    [Fact]
    public async Task Patch_Me_SemAutenticacao_Retorna401()
    {
        var client = _factory.CreateClient();
        var response = await client.PatchAsJsonAsync("/usuarios/me", new { nome = "Maria" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Patch_Me_DadosValidos_Retorna200()
    {
        _factory.AtualizarHandlerMock.Setup(h => h.HandleAsync(It.IsAny<AtualizarUsuarioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaUsuario);

        var response = await CriarClienteAutenticado().PatchAsJsonAsync("/usuarios/me",
            new { nome = "Maria" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_Me_NomeMuitoLongo_Retorna400()
    {
        var response = await CriarClienteAutenticado().PatchAsJsonAsync("/usuarios/me",
            new { nome = new string('a', 101) });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_Me_UsuarioInativo_Retorna403()
    {
        _factory.AtualizarHandlerMock.Setup(h => h.HandleAsync(It.IsAny<AtualizarUsuarioCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UsuarioInativoException());

        var response = await CriarClienteAutenticado().PatchAsJsonAsync("/usuarios/me",
            new { nome = "Maria" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Patch_Me_FotoUrlMuitoLonga_Retorna400()
    {
        var response = await CriarClienteAutenticado().PatchAsJsonAsync("/usuarios/me",
            new { fotoUrl = new string('a', 501) });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://files.example.com/img.jpg")]
    public async Task Patch_Me_FotoUrlSchemeInvalido_Retorna400(string fotoUrl)
    {
        var response = await CriarClienteAutenticado().PatchAsJsonAsync("/usuarios/me",
            new { fotoUrl });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_Me_BioMuitoLonga_Retorna400()
    {
        var response = await CriarClienteAutenticado().PatchAsJsonAsync("/usuarios/me",
            new { bio = new string('a', 501) });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- PATCH /usuarios/{id}/status ---

    [Fact]
    public async Task Patch_Status_SemAutenticacao_Retorna401()
    {
        var client = _factory.CreateClient();
        var response = await client.PatchAsJsonAsync($"/usuarios/{Guid.NewGuid()}/status",
            new { status = "Inativo" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Patch_Status_AdminValido_Retorna200()
    {
        _factory.AlterarStatusHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AlterarStatusUsuarioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaUsuario);

        var response = await CriarClienteAutenticado().PatchAsJsonAsync(
            $"/usuarios/{Guid.NewGuid()}/status", new { status = "Inativo" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_Status_TrainerSemPermissao_Retorna403()
    {
        _factory.AlterarStatusHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AlterarStatusUsuarioCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AcessoNegadoException());

        var response = await CriarClienteAutenticado().PatchAsJsonAsync(
            $"/usuarios/{Guid.NewGuid()}/status", new { status = "Inativo" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Patch_Status_UsuarioNaoEncontrado_Retorna404()
    {
        _factory.AlterarStatusHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AlterarStatusUsuarioCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UsuarioNaoEncontradoException());

        var response = await CriarClienteAutenticado().PatchAsJsonAsync(
            $"/usuarios/{Guid.NewGuid()}/status", new { status = "Inativo" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_Registrar_EmailMuitoLongo_Retorna400()
    {
        var email = new string('a', 251) + "@b.com";
        var response = await CriarClienteAutenticado().PostAsJsonAsync("/usuarios/registrar",
            new { nome = "João", email, tenantNome = "Academia" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- WebApplicationFactory ---

    public class ForzionWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ObterUsuarioAtualHandler> ObterHandlerMock { get; } = new(
            Mock.Of<IUsuarioRepository>(),
            Mock.Of<ILogger<ObterUsuarioAtualHandler>>());

        public Mock<RegistrarUsuarioHandler> RegistrarHandlerMock { get; } = new(
            Mock.Of<IUsuarioRepository>(),
            Mock.Of<ITenantRepository>(),
            Mock.Of<IPlanoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ILogger<RegistrarUsuarioHandler>>());

        public Mock<AtualizarUsuarioHandler> AtualizarHandlerMock { get; } = new(
            Mock.Of<IUsuarioRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ILogger<AtualizarUsuarioHandler>>());

        public Mock<AlterarStatusUsuarioHandler> AlterarStatusHandlerMock { get; } = new(
            Mock.Of<IUsuarioRepository>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ILogger<AlterarStatusUsuarioHandler>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ObterUsuarioAtualHandler>();
                services.RemoveAll<RegistrarUsuarioHandler>();
                services.RemoveAll<AtualizarUsuarioHandler>();
                services.RemoveAll<AlterarStatusUsuarioHandler>();

                services.AddScoped(_ => ObterHandlerMock.Object);
                services.AddScoped(_ => RegistrarHandlerMock.Object);
                services.AddScoped(_ => AtualizarHandlerMock.Object);
                services.AddScoped(_ => AlterarStatusHandlerMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }
    }

    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(header))
                return Task.FromResult(AuthenticateResult.Fail("Sem token"));

            var userId = header.Replace("Test ", "");
            if (!Guid.TryParse(userId, out _))
                return Task.FromResult(AuthenticateResult.Fail("Token inválido"));

            var claims = new[] { new Claim("sub", userId) };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
