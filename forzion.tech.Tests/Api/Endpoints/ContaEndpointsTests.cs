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
using forzion.tech.Application.UseCases.Conta.Lgpd;
using forzion.tech.Application.UseCases.Conta.Logout;
using forzion.tech.Application.UseCases.Conta.ObterPerfil;
using forzion.tech.Domain.Shared;
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
            .ReturnsAsync(Result.Success());

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
            .ReturnsAsync(Result.Success());

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
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/conta/logout", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // --- GET /conta/lgpd/exportar ---

    [Fact]
    public async Task Get_ExportarLgpd_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/conta/lgpd/exportar");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_ExportarLgpd_SemFormato_Retorna200Json()
    {
        var export = CriarExportFake();
        _factory.ExportarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExportarDadosPessoaisCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(export));

        var response = await CriarClienteAutenticado().GetAsync("/conta/lgpd/exportar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Get_ExportarLgpd_FormatoXlsx_Retorna200ComArquivoXlsx()
    {
        var export = CriarExportFake();
        var fakeBytes = new byte[] { 1, 2, 3 };

        _factory.ExportarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ExportarDadosPessoaisCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(export));
        _factory.ExcelRendererMock
            .Setup(r => r.Render(It.IsAny<DadosPessoaisExport>()))
            .Returns(fakeBytes);

        var response = await CriarClienteAutenticado().GetAsync("/conta/lgpd/exportar?formato=xlsx");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("meus-dados.xlsx");
    }

    private static DadosPessoaisExport CriarExportFake() =>
        new("1.0", DateTime.UtcNow,
            new ContaExportDto(ContaId, "user@test.com", "Treinador", false, null, DateTime.UtcNow),
            null, null, [], [], [], [], [], [], [], []);

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
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<IValidator<AtualizarPerfilCommand>>());

        public Mock<AlterarSenhaHandler> AlterarSenhaHandlerMock { get; } = new(
            Mock.Of<IUserContext>(),
            Mock.Of<IContaRepository>(),
            Mock.Of<IPasswordHasher>(),
            Mock.Of<IRefreshTokenService>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<IValidator<AlterarSenhaCommand>>());

        public Mock<LogoutHandler> LogoutHandlerMock { get; } = new(
            Mock.Of<ITokenRevogadoRepository>(),
            Mock.Of<IRefreshTokenService>(),
            Mock.Of<IUserContext>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<ILogger<LogoutHandler>>());

        public Mock<ExportarDadosPessoaisHandler> ExportarHandlerMock { get; } = new(
            Mock.Of<IContaRepository>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<IAssinaturaAlunoRepository>(),
            Mock.Of<IPagamentoRepository>(),
            Mock.Of<IPacoteRepository>(),
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IExecucaoTreinoRepository>(),
            Mock.Of<IEmailDeliveryLogRepository>(),
            Mock.Of<IWhatsAppDeliveryLogRepository>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System);

        public Mock<IDadosPessoaisExcelRenderer> ExcelRendererMock { get; } = new();

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
                services.RemoveAll<ExportarDadosPessoaisHandler>();
                services.RemoveAll<IDadosPessoaisExcelRenderer>();
                services.RemoveAll<IUserContext>();

                services.AddScoped(_ => ObterPerfilHandlerMock.Object);
                services.AddScoped(_ => AtualizarPerfilHandlerMock.Object);
                services.AddScoped(_ => AlterarSenhaHandlerMock.Object);
                services.AddScoped(_ => LogoutHandlerMock.Object);
                services.AddScoped(_ => ExportarHandlerMock.Object);
                services.AddScoped<IDadosPessoaisExcelRenderer>(_ => ExcelRendererMock.Object);

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
