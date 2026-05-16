using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using forzion.tech.AI.Clients;
using forzion.tech.AI.Configuration;
using forzion.tech.AI.GuardRails;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos;
using forzion.tech.Application.UseCases.Treinos.CriarTreino;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.AI.Endpoints;

public class TreinadorAssistantEndpointsTests : IClassFixture<TreinadorAssistantEndpointsTests.AssistantWebFactory>
{
    private readonly AssistantWebFactory _factory;
    private static readonly Guid TreinadorId = Guid.NewGuid();

    public TreinadorAssistantEndpointsTests(AssistantWebFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CriarClienteAutenticado()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", TreinadorId.ToString());
        return client;
    }

    // ── ApplySuggestion ──────────────────────────────────────────────────────

    [Fact]
    public async Task ApplySuggestion_DraftNaoEncontrado_Retorna404()
    {
        _factory.DraftServiceMock
            .Setup(d => d.GetDraft(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns((SugestaoDraft?)null);

        var resp = await CriarClienteAutenticado()
            .PostAsJsonAsync("/treinador/assistant/apply-suggestion",
                new { draftId = Guid.NewGuid() });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ApplySuggestion_DraftValido_CriaTreinoERetorna200()
    {
        var draftId = Guid.NewGuid();
        var draft = new SugestaoDraft(
            TreinadorId, Guid.NewGuid(), "Hipertrofia", "Intermediário", 3,
            DateTime.UtcNow.AddMinutes(10));

        _factory.DraftServiceMock
            .Setup(d => d.GetDraft(draftId, TreinadorId))
            .Returns(draft);

        var treinoId = Guid.NewGuid();
        _factory.CriarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarTreinoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TreinoResponse(
                treinoId, "Sugestão IA: Hipertrofia (Intermediário)",
                ObjetivoTreino.Hipertrofia, DificuldadeTreino.Intermediario,
                null, null, TreinadorId, [], DateTime.UtcNow, null));

        var resp = await CriarClienteAutenticado()
            .PostAsJsonAsync("/treinador/assistant/apply-suggestion",
                new { draftId });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.DraftServiceMock.Verify(d => d.RemoveDraft(draftId), Times.Once);
        _factory.CriarHandlerMock.Verify(
            h => h.HandleAsync(
                It.Is<CriarTreinoCommand>(c =>
                    c.TreinadorId == TreinadorId &&
                    c.AlunoId == draft.AlunoId &&
                    c.Objetivo == ObjetivoTreino.Hipertrofia),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplySuggestion_SemAutenticacao_Retorna401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync(
            "/treinador/assistant/apply-suggestion", new { draftId = Guid.NewGuid() });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApplySuggestion_ObjetivoDificuldade_Mapeados()
    {
        var draftId = Guid.NewGuid();
        CriarTreinoCommand? captured = null;

        _factory.DraftServiceMock
            .Setup(d => d.GetDraft(draftId, TreinadorId))
            .Returns(new SugestaoDraft(
                TreinadorId, Guid.NewGuid(), "emagrecimento", "Avançado", 5,
                DateTime.UtcNow.AddMinutes(10)));

        _factory.CriarHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CriarTreinoCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CriarTreinoCommand, CancellationToken>((cmd, _) => captured = cmd)
            .ReturnsAsync(new TreinoResponse(
                Guid.NewGuid(), "x", ObjetivoTreino.Emagrecimento, DificuldadeTreino.Avancado,
                null, null, TreinadorId, [], DateTime.UtcNow, null));

        await CriarClienteAutenticado()
            .PostAsJsonAsync("/treinador/assistant/apply-suggestion", new { draftId });

        captured.Should().NotBeNull();
        captured!.Objetivo.Should().Be(ObjetivoTreino.Emagrecimento);
        captured.Dificuldade.Should().Be(DificuldadeTreino.Avancado);
    }

    // ── WebApplicationFactory ─────────────────────────────────────────────────

    public class AssistantWebFactory : WebApplicationFactory<Program>
    {
        public Mock<IDraftSuggestionService> DraftServiceMock { get; } = new();

        public Mock<CriarTreinoHandler> CriarHandlerMock { get; } = new(
            Mock.Of<forzion.tech.Application.Interfaces.Repositories.ITreinoRepository>(),
            Mock.Of<forzion.tech.Application.Interfaces.Repositories.ITreinoAlunoRepository>(),
            Mock.Of<forzion.tech.Application.Interfaces.Repositories.IAlunoRepository>(),
            Mock.Of<forzion.tech.Application.Interfaces.Repositories.IVinculoTreinadorAlunoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserContext>(),
            Mock.Of<FluentValidation.IValidator<CriarTreinoCommand>>(),
            Mock.Of<ILogger<CriarTreinoHandler>>())
        { CallBase = false };

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                // Register repos as mocks so AlunoTools/TreinadorTools can be injected
                services.AddScoped(_ => Mock.Of<IAlunoRepository>());
                services.AddScoped(_ => Mock.Of<IExercicioRepository>());
                services.AddScoped(_ => Mock.Of<IExecucaoTreinoRepository>());
                services.AddScoped(_ => Mock.Of<ITreinoAlunoRepository>());
                services.AddScoped(_ => Mock.Of<IVinculoTreinadorAlunoRepository>());

                // Register AI services (AgentRegistry, ITokenBudget, IDraftRequestTracker, etc.)
                services.AddForzionAI();

                // Override chat client factory — no real API key needed in tests
                services.RemoveAll<IChatClientFactory>();
                var chatClientMock = new Mock<IChatClient>();
                services.AddSingleton<IChatClientFactory>(_ =>
                {
                    var factoryMock = new Mock<IChatClientFactory>();
                    factoryMock.Setup(f => f.CreateInternalClient()).Returns(chatClientMock.Object);
                    return factoryMock.Object;
                });

                // Override IDraftSuggestionService with our controllable mock
                services.RemoveAll<IDraftSuggestionService>();
                services.AddSingleton(_ => DraftServiceMock.Object);

                services.RemoveAll<CriarTreinoHandler>();
                services.AddScoped(_ => CriarHandlerMock.Object);

                services.RemoveAll<IUserContext>();
                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.PerfilId).Returns(TreinadorId);
                userContextMock.Setup(u => u.IsSystemAdmin).Returns(false);
                services.AddScoped(_ => userContextMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TreinadorAuthHandler>("Test", _ => { });
            });
        }
    }

    public class TreinadorAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TreinadorAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(header))
                return Task.FromResult(AuthenticateResult.Fail("Sem token"));

            var perfilId = header.Replace("Test ", "");
            var claims = new[]
            {
                new Claim("perfil_id", perfilId),
                new Claim("tipo_conta", "Treinador"),
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
