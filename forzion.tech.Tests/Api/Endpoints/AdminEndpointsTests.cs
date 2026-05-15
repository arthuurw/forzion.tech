using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Application.UseCases.Admin.GruposMusculares;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.AtualizarGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.CriarGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ExcluirGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ListarGruposMusculares;
using forzion.tech.Application.UseCases.Planos;
using forzion.tech.Application.UseCases.Planos.AtualizarPlanoTreinador;
using forzion.tech.Application.UseCases.Planos.CriarPlanoTreinador;
using forzion.tech.Application.UseCases.Planos.ExcluirPlanoTreinador;
using forzion.tech.Application.UseCases.Planos.ListarPlanosTreinador;
using forzion.tech.Application.UseCases.Treinadores;
using forzion.tech.Application.UseCases.Treinadores.AprovarTreinador;
using forzion.tech.Application.UseCases.Treinadores.AtribuirPlano;
using forzion.tech.Application.UseCases.Treinadores.ExcluirTreinador;
using forzion.tech.Application.UseCases.Treinadores.InativarTreinador;
using forzion.tech.Application.UseCases.Treinadores.ListarTreinadores;
using forzion.tech.Application.UseCases.Treinadores.ReprovarTreinador;
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
using FluentValidation;

namespace forzion.tech.Tests.Api.Endpoints;

public class AdminEndpointsTests : IClassFixture<AdminEndpointsTests.AdminWebFactory>
{
    private readonly AdminWebFactory _factory;

    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();

    private static readonly TreinadorResponse RespostaTreinador = new(
        TreinadorId, Guid.NewGuid(), "Carlos", TreinadorStatus.AguardandoAprovacao, null, DateTime.UtcNow);

    private static readonly PlanoTreinadorResponse RespostaPlano = new(
        Guid.NewGuid(), "Starter", 10, 99m, true, DateTime.UtcNow, null);

    private static readonly GrupoMuscularResponse RespostaGrupo = new(
        Guid.NewGuid(), "Peitoral", DateTime.UtcNow, null);

    public AdminEndpointsTests(AdminWebFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CriarClienteAdmin()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "admin");
        return client;
    }

    private HttpClient CriarClienteNaoAdmin()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "treinador");
        return client;
    }

    // --- GET /admin/treinadores ---

    [Fact]
    public async Task Get_Treinadores_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/admin/treinadores");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Treinadores_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin().GetAsync("/admin/treinadores");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Treinadores_Admin_Retorna200()
    {
        var lista = new ListarTreinadoresResponse(new[] { RespostaTreinador }, 1, 1, 20);
        _factory.ListarTreinadoresHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<TreinadorStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lista);

        var response = await CriarClienteAdmin().GetAsync("/admin/treinadores");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Get_Treinadores_PaginaInvalida_Retorna400()
    {
        var response = await CriarClienteAdmin().GetAsync("/admin/treinadores?pagina=0");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Treinadores_TamanhoPaginaInvalido_Retorna400()
    {
        var response = await CriarClienteAdmin().GetAsync("/admin/treinadores?tamanhoPagina=200");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- POST /admin/treinadores/{id}/aprovar ---

    [Fact]
    public async Task Post_AprovarTreinador_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/aprovar", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_AprovarTreinador_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/aprovar", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_AprovarTreinador_Admin_Retorna200()
    {
        var aprovado = RespostaTreinador with { Status = TreinadorStatus.Ativo };
        _factory.AprovarTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AprovarTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(aprovado));

        var response = await CriarClienteAdmin()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/aprovar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_AprovarTreinador_NaoEncontrado_Retorna404()
    {
        _factory.AprovarTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AprovarTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TreinadorNaoEncontradoException());

        var response = await CriarClienteAdmin()
            .PostAsJsonAsync($"/admin/treinadores/{Guid.NewGuid()}/aprovar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- POST /admin/treinadores/{id}/inativar ---

    [Fact]
    public async Task Post_InativarTreinador_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/inativar", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_InativarTreinador_Admin_Retorna204()
    {
        _factory.InativarTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<InativarTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteAdmin()
            .PostAsJsonAsync($"/admin/treinadores/{TreinadorId}/inativar", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // --- DELETE /admin/treinadores/{id} ---

    [Fact]
    public async Task Delete_Treinador_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient()
            .DeleteAsync($"/admin/treinadores/{TreinadorId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Treinador_Admin_Retorna204()
    {
        _factory.ExcluirTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await CriarClienteAdmin()
            .DeleteAsync($"/admin/treinadores/{TreinadorId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_Treinador_NaoEncontrado_Retorna404()
    {
        _factory.ExcluirTreinadorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExcluirTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TreinadorNaoEncontradoException());

        var response = await CriarClienteAdmin()
            .DeleteAsync($"/admin/treinadores/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GET /admin/planos ---

    [Fact]
    public async Task Get_Planos_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/admin/planos");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Planos_Admin_Retorna200()
    {
        _factory.ListarPlanosHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { RespostaPlano });

        var response = await CriarClienteAdmin().GetAsync("/admin/planos");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /admin/planos ---

    [Fact]
    public async Task Post_CriarPlano_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/admin/planos", new { nome = "Pro", maxAlunos = 50, preco = 199m });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_CriarPlano_Admin_Retorna201()
    {
        _factory.CriarPlanoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarPlanoTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaPlano);

        var response = await CriarClienteAdmin()
            .PostAsJsonAsync("/admin/planos", new { nome = "Pro", maxAlunos = 50, preco = 199m });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- GET /admin/grupos-musculares ---

    [Fact]
    public async Task Get_GruposMusculares_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/admin/grupos-musculares");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_GruposMusculares_Admin_Retorna200()
    {
        _factory.ListarGruposHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { RespostaGrupo });

        var response = await CriarClienteAdmin().GetAsync("/admin/grupos-musculares");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /admin/grupos-musculares ---

    [Fact]
    public async Task Post_CriarGrupoMuscular_NaoAdmin_Retorna403()
    {
        var response = await CriarClienteNaoAdmin()
            .PostAsJsonAsync("/admin/grupos-musculares", new { nome = "Ombros" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_CriarGrupoMuscular_Admin_Retorna201()
    {
        _factory.CriarGrupoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarGrupoMuscularCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaGrupo);

        var response = await CriarClienteAdmin()
            .PostAsJsonAsync("/admin/grupos-musculares", new { nome = "Ombros" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- PATCH /admin/treinadores/{id}/plano ---

    [Fact]
    public async Task Patch_AtribuirPlano_Admin_Retorna200()
    {
        var atribuido = RespostaTreinador with { PlanoTreinadorId = Guid.NewGuid() };
        _factory.AtribuirPlanoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<AtribuirPlanoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(atribuido);

        var response = await CriarClienteAdmin()
            .PatchAsJsonAsync($"/admin/treinadores/{TreinadorId}/plano", new { planoId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- WebApplicationFactory ---

    public class AdminWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ListarTreinadoresHandler> ListarTreinadoresHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>());

        public Mock<AprovarTreinadorHandler> AprovarTreinadorHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ILogger<AprovarTreinadorHandler>>());

        public Mock<ReprovarTreinadorHandler> ReprovarTreinadorHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ILogger<ReprovarTreinadorHandler>>());

        public Mock<InativarTreinadorHandler> InativarTreinadorHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IPacoteAlunoRepository>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ILogger<InativarTreinadorHandler>>());

        public Mock<ExcluirTreinadorHandler> ExcluirTreinadorHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<ILogger<ExcluirTreinadorHandler>>());

        public Mock<AtribuirPlanoHandler> AtribuirPlanoHandlerMock { get; } = new(
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IPlanoTreinadorRepository>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ILogger<AtribuirPlanoHandler>>());

        public Mock<ListarPlanosTreinadorHandler> ListarPlanosHandlerMock { get; } = new(
            Mock.Of<IPlanoTreinadorRepository>());

        public Mock<CriarPlanoTreinadorHandler> CriarPlanoHandlerMock { get; } = new(
            Mock.Of<IPlanoTreinadorRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<CriarPlanoTreinadorCommand>>(),
            Mock.Of<IUserContext>(),
            Mock.Of<ILogger<CriarPlanoTreinadorHandler>>());

        public Mock<ListarGruposMuscularesHandler> ListarGruposHandlerMock { get; } = new(
            Mock.Of<IGrupoMuscularRepository>());

        public Mock<CriarGrupoMuscularHandler> CriarGrupoHandlerMock { get; } = new(
            Mock.Of<IGrupoMuscularRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IValidator<CriarGrupoMuscularCommand>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ListarTreinadoresHandler>();
                services.RemoveAll<AprovarTreinadorHandler>();
                services.RemoveAll<ReprovarTreinadorHandler>();
                services.RemoveAll<InativarTreinadorHandler>();
                services.RemoveAll<ExcluirTreinadorHandler>();
                services.RemoveAll<AtribuirPlanoHandler>();
                services.RemoveAll<ListarPlanosTreinadorHandler>();
                services.RemoveAll<CriarPlanoTreinadorHandler>();
                services.RemoveAll<ListarGruposMuscularesHandler>();
                services.RemoveAll<CriarGrupoMuscularHandler>();
                services.RemoveAll<IUserContext>();

                services.AddScoped(_ => ListarTreinadoresHandlerMock.Object);
                services.AddScoped(_ => AprovarTreinadorHandlerMock.Object);
                services.AddScoped(_ => ReprovarTreinadorHandlerMock.Object);
                services.AddScoped(_ => InativarTreinadorHandlerMock.Object);
                services.AddScoped(_ => ExcluirTreinadorHandlerMock.Object);
                services.AddScoped(_ => AtribuirPlanoHandlerMock.Object);
                services.AddScoped(_ => ListarPlanosHandlerMock.Object);
                services.AddScoped(_ => CriarPlanoHandlerMock.Object);
                services.AddScoped(_ => ListarGruposHandlerMock.Object);
                services.AddScoped(_ => CriarGrupoHandlerMock.Object);

                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.ContaId).Returns(AdminId);
                userContextMock.Setup(u => u.PerfilId).Returns(AdminId);
                services.AddScoped(_ => userContextMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, AdminTestAuthHandler>("Test", _ => { });
            });
        }
    }

    public class AdminTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public AdminTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(header))
                return Task.FromResult(AuthenticateResult.Fail("Sem token"));

            var param = header.Replace("Test ", "");
            if (string.IsNullOrEmpty(param))
                return Task.FromResult(AuthenticateResult.Fail("Token inválido"));

            string userId;
            string tipoConta;

            if (param == "admin")
            {
                userId = AdminId.ToString();
                tipoConta = "SystemAdmin";
            }
            else if (param == "treinador")
            {
                userId = Guid.NewGuid().ToString();
                tipoConta = "Treinador";
            }
            else
            {
                return Task.FromResult(AuthenticateResult.Fail("Token inválido"));
            }

            var claims = new[]
            {
                new Claim("sub", userId),
                new Claim("tipo_conta", tipoConta)
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
