using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.AlterarStatusAluno;
using forzion.tech.Application.UseCases.Alunos.AtualizarAluno;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;
using forzion.tech.Application.UseCases.Alunos.ObterAluno;
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
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace forzion.tech.Tests.Api.Endpoints;

public class AlunoEndpointsTests : IClassFixture<AlunoEndpointsTests.AlunoWebFactory>
{
    private readonly AlunoWebFactory _factory;
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private static readonly Guid PerfilId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();

    private static readonly AlunoResponse RespostaAluno = new(
        AlunoId, "João", null, null, AlunoStatus.Ativo, UsuarioId,
        DateTime.UtcNow, null);

    public AlunoEndpointsTests(AlunoWebFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CriarClienteAutenticado()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", UsuarioId.ToString());
        return client;
    }

    // --- POST /alunos ---

    [Fact]
    public async Task Post_Alunos_SemAutenticacao_Retorna401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/alunos", new { nome = "João" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Alunos_DadosValidos_Retorna201()
    {
        _factory.CadastrarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CadastrarAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaAluno);

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/alunos",
            new { nome = "João" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task Post_Alunos_NomeVazio_Retorna400()
    {
        _factory.CadastrarHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CadastrarAlunoCommand>(), It.IsAny<CancellationToken>()))
            .Returns((CadastrarAlunoCommand c, CancellationToken ct) => throw new FluentValidation.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("Nome", "Nome é obrigatório") }));

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/alunos",
            new { nome = "" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Alunos_NomeMuitoLongo_Retorna400()
    {
        _factory.CadastrarHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CadastrarAlunoCommand>(), It.IsAny<CancellationToken>()))
            .Returns((CadastrarAlunoCommand c, CancellationToken ct) => throw new FluentValidation.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("Nome", "Nome muito longo") }));

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/alunos",
            new { nome = new string('a', 101) });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Alunos_EmailInvalido_Retorna400()
    {
        _factory.CadastrarHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CadastrarAlunoCommand>(), It.IsAny<CancellationToken>()))
            .Returns((CadastrarAlunoCommand c, CancellationToken ct) => throw new FluentValidation.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("Email", "Email inválido") }));

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/alunos",
            new { nome = "João", email = "invalido" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_Alunos_TelefoneMuitoLongo_Retorna400()
    {
        var response = await CriarClienteAutenticado().PostAsJsonAsync("/alunos",
            new { nome = "João", telefone = new string('1', 21) });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- GET /alunos ---

    [Fact]
    public async Task Get_Alunos_SemAutenticacao_Retorna401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/alunos");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Alunos_Autenticado_Retorna200()
    {
        var lista = new ListarAlunosResponse(new[] { RespostaAluno }, 1, 1, 20);
        _factory.ListarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ListarAlunosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lista);

        var response = await CriarClienteAutenticado().GetAsync("/alunos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().Should().Be(1);
    }

    // --- GET /alunos/{id} ---

    [Fact]
    public async Task Get_AlunoById_SemAutenticacao_Retorna401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/alunos/{AlunoId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_AlunoById_Encontrado_Retorna200()
    {
        _factory.ObterHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterAlunoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaAluno);

        var response = await CriarClienteAutenticado().GetAsync($"/alunos/{AlunoId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("alunoId").GetGuid().Should().Be(AlunoId);
    }

    [Fact]
    public async Task Get_AlunoById_NaoEncontrado_Retorna404()
    {
        _factory.ObterHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterAlunoQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AlunoNaoEncontradoException());

        var response = await CriarClienteAutenticado().GetAsync($"/alunos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- PATCH /alunos/{id} ---

    [Fact]
    public async Task Patch_Aluno_SemAutenticacao_Retorna401()
    {
        var client = _factory.CreateClient();
        var response = await client.PatchAsJsonAsync($"/alunos/{AlunoId}", new { nome = "Maria" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Patch_Aluno_DadosValidos_Retorna200()
    {
        _factory.AtualizarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaAluno);

        var response = await CriarClienteAutenticado().PatchAsJsonAsync($"/alunos/{AlunoId}",
            new { nome = "Maria" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_Aluno_NomeMuitoLongo_Retorna400()
    {
        _factory.AtualizarHandlerMock.Setup(h => h.HandleAsync(It.IsAny<AtualizarAlunoCommand>(), It.IsAny<CancellationToken>()))
            .Returns((AtualizarAlunoCommand c, CancellationToken ct) => throw new FluentValidation.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("Nome", "Nome muito longo") }));

        var response = await CriarClienteAutenticado().PatchAsJsonAsync($"/alunos/{AlunoId}",
            new { nome = new string('a', 101) });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_Aluno_AlunoInativo_Retorna403()
    {
        _factory.AtualizarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AlunoInativoException());

        var response = await CriarClienteAutenticado().PatchAsJsonAsync($"/alunos/{AlunoId}",
            new { nome = "Maria" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- PATCH /alunos/{id}/status ---

    [Fact]
    public async Task Patch_AlunoStatus_SemAutenticacao_Retorna401()
    {
        var client = _factory.CreateClient();
        var response = await client.PatchAsJsonAsync($"/alunos/{AlunoId}/status",
            new { status = "Inativo" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Patch_AlunoStatus_AdminValido_Retorna200()
    {
        _factory.AlterarStatusHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AlterarStatusAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaAluno);

        var response = await CriarClienteAutenticado().PatchAsJsonAsync($"/alunos/{AlunoId}/status",
            new { status = "Inativo" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_AlunoStatus_TrainerSemPermissao_Retorna403()
    {
        _factory.AlterarStatusHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AlterarStatusAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AcessoNegadoException());

        var response = await CriarClienteAutenticado().PatchAsJsonAsync($"/alunos/{AlunoId}/status",
            new { status = "Inativo" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Patch_AlunoStatus_AlunoNaoEncontrado_Retorna404()
    {
        _factory.AlterarStatusHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AlterarStatusAlunoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AlunoNaoEncontradoException());

        var response = await CriarClienteAutenticado().PatchAsJsonAsync($"/alunos/{AlunoId}/status",
            new { status = "Inativo" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- WebApplicationFactory ---

    public class AlunoWebFactory : WebApplicationFactory<Program>
    {
        private static readonly IValidator<CadastrarAlunoCommand> CadastrarValidator = 
            new CadastrarAlunoCommandValidator();

        public Mock<CadastrarAlunoHandler> CadastrarHandlerMock { get; } = new(
            Mock.Of<IAlunoRepository>(),
            Mock.Of<IUnitOfWork>(),
            CadastrarValidator,
            Mock.Of<ILogger<CadastrarAlunoHandler>>())
        {
            CallBase = true
        };

        public Mock<ObterAlunoHandler> ObterHandlerMock { get; } = new(
            Mock.Of<IAlunoRepository>(),
            Mock.Of<ILogger<ObterAlunoHandler>>());

        public Mock<ListarAlunosHandler> ListarHandlerMock { get; } = new(
            Mock.Of<IAlunoRepository>(),
            Mock.Of<ILogger<ListarAlunosHandler>>());

        public Mock<AtualizarAlunoHandler> AtualizarHandlerMock { get; } = new(
            Mock.Of<IAlunoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ILogger<AtualizarAlunoHandler>>());

        public Mock<AlterarStatusAlunoHandler> AlterarStatusHandlerMock { get; } = new(
            Mock.Of<IAlunoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ILogger<AlterarStatusAlunoHandler>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<CadastrarAlunoHandler>();
                services.RemoveAll<ObterAlunoHandler>();
                services.RemoveAll<ListarAlunosHandler>();
                services.RemoveAll<AtualizarAlunoHandler>();
                services.RemoveAll<AlterarStatusAlunoHandler>();
                services.RemoveAll<IUserContext>();

                services.AddScoped(_ => CadastrarHandlerMock.Object);
                services.AddScoped(_ => ObterHandlerMock.Object);
                services.AddScoped(_ => ListarHandlerMock.Object);
                services.AddScoped(_ => AtualizarHandlerMock.Object);
                services.AddScoped(_ => AlterarStatusHandlerMock.Object);

                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.PerfilId).Returns(PerfilId);
                services.AddScoped(_ => userContextMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, AlunoTestAuthHandler>("Test", _ => { });
            });
        }
    }

    public class AlunoTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public AlunoTestAuthHandler(
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
