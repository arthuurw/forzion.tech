using System.Security.Claims;
using System.Text.Encodings.Web;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PactNet.Verifier;

namespace forzion.tech.PactVerification;

public class ForzionApiProviderTests : IClassFixture<ForzionApiProviderFactory>
{
    private readonly ForzionApiProviderFactory _factory;

    public ForzionApiProviderTests(ForzionApiProviderFactory factory) => _factory = factory;

    [Fact]
    public void VerificaContratosPublicadosNoBroker()
    {
        var brokerUrl = Environment.GetEnvironmentVariable("PACT_BROKER_BASE_URL");
        if (string.IsNullOrWhiteSpace(brokerUrl))
        {
            // Sem broker configurado -> no-op (mantem o build/test verde fora do
            // workflow pact-provider).
            return;
        }

        var brokerUser = Environment.GetEnvironmentVariable("PACT_BROKER_USERNAME") ?? "";
        var brokerPass = Environment.GetEnvironmentVariable("PACT_BROKER_PASSWORD") ?? "";
        var providerVersion = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local-dev";
        var publish = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

        // WebApplicationFactory e lazy: acessar Services forca CreateHost (sobe o
        // Kestrel e popula ServerUri). NAO usamos CreateClient — ele passa pelo
        // TestServer in-memory, que nao existe nesta factory (so Kestrel real).
        _ = _factory.Services;

        var config = new PactVerifierConfig();
        using var verifier = new PactVerifier(config);

        verifier
            .ServiceProvider("forzion-api", new Uri(_factory.ServerUri))
            .WithPactBrokerSource(new Uri(brokerUrl), options =>
            {
                if (!string.IsNullOrEmpty(brokerUser))
                {
                    options.BasicAuthentication(brokerUser, brokerPass);
                }

                if (publish)
                {
                    options.PublishResults(providerVersion, results => results.ProviderBranch("homolog"));
                }
            })
            // F5b — broker chama esse URL antes de cada interacao com o "given"
            // declarado pelo consumer. Provider seta o state no singleton e o
            // middleware (ProviderStateMiddleware) curto-circuita responses pra
            // 401/404/500 ProblemDetails quando o state casa cenario de erro.
            // (Chamada APOS WithPactBrokerSource — vive em IPactVerifierConfigured.)
            .WithProviderStateUrl(new Uri(new Uri(_factory.ServerUri), "/_pact/provider-states"))
            .Verify();
    }
}

/// <summary>
/// Hospeda a API com os REPOSITORIOS dos contratos mockados (handlers reais).
/// Auth de teste por path. Sem DB: Program sobe em env Test sem conexao real;
/// os repos mockados retornam shapes representativos e os handlers materializam
/// os DTOs de resposta — mudanca de shape no handler ou na conversao para DTO
/// quebra o Pact (F4 fase 3 test remediation).
///
/// O verifier nativo do Pact faz HTTP real, entao TestServer (in-memory) nao
/// serve: o truque de 2 hosts inicia um Kestrel numa porta livre.
/// </summary>
public class ForzionApiProviderFactory : WebApplicationFactory<Program>
{
    private IHost? _kestrelHost;

    public string ServerUri { get; private set; } = string.Empty;

    // ContaId fixo para que IUserContext + IContaRepository batam (handler de
    // perfil chama contaRepository.ObterPorIdAsync(userContext.ContaId)).
    private static readonly Guid TestContaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    /// <summary>State holder pra provider state handlers (F5b). Thread-safe simples.</summary>
    public sealed class ProviderStateContext
    {
        private string? _currentState;
        private readonly object _lock = new();

        public string? Current
        {
            get { lock (_lock) return _currentState; }
            set { lock (_lock) _currentState = value; }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("AllowedHosts", "*");
        builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

        builder.ConfigureServices(services =>
        {
            // F4: TODOS os repos relevantes mockados no nivel REPO (nao handler).
            // Handlers reais executam e materializam os response DTOs — mudanca de
            // shape nos handlers ou na conversao quebra o Pact (ao inves de passar
            // silenciosamente pelos mocks de handler antigos).
            services.RemoveAll<IAlunoRepository>();
            services.RemoveAll<IContaRepository>();
            services.RemoveAll<ITreinadorRepository>();
            services.RemoveAll<IVinculoTreinadorAlunoRepository>();
            services.RemoveAll<ITreinoAlunoRepository>();
            services.RemoveAll<IExercicioRepository>();
            services.RemoveAll<IUserContext>();

            services.AddScoped(_ => BuildAlunoRepositoryMock());
            services.AddScoped(_ => BuildContaRepositoryMock());
            services.AddScoped(_ => BuildTreinadorRepositoryMock());
            services.AddScoped(_ => BuildVinculoRepositoryMock());
            services.AddScoped(_ => BuildTreinoAlunoRepositoryMock());
            services.AddScoped(_ => BuildExercicioRepositoryMock());
            services.AddScoped(_ => BuildUserContextMock());

            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, PathRoleAuthHandler>("Test", _ => { });

            // F5b — Provider state handlers pra Pact error contracts.
            // Singleton state guarda o que o broker mandou via /_pact/provider-states.
            // IStartupFilter insere middleware que (a) handle o endpoint de state e
            // (b) curto-circuita request com ProblemDetails quando o state casa um
            // cenario de erro (401/404/500).
            services.AddSingleton<ProviderStateContext>();
            services.AddTransient<IStartupFilter, ProviderStateStartupFilter>();
        });
    }

    // Padrao "2 hosts" (Andrew Lock): o WAF EXIGE um TestServer (faz cast de IServer
    // -> TestServer no EnsureServer), mas o verifier do Pact faz HTTP real e precisa
    // de um Kestrel numa porta livre. Entao subimos os dois: retornamos o testHost
    // (TestServer) pro WAF e mantemos um Kestrel separado, cujo endereco o Pact usa.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();

        builder.ConfigureWebHost(webBuilder => webBuilder
            .UseKestrel()
            .UseUrls("http://127.0.0.1:0"));
        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        var addresses = _kestrelHost.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("IServerAddressesFeature indisponivel apos start do Kestrel.");
        ServerUri = addresses.Addresses.Last();

        testHost.Start();
        return testHost;
    }

    protected override void Dispose(bool disposing)
    {
        _kestrelHost?.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>
    /// Conta autenticada do tipo Aluno. <c>ObterPerfilHandler</c> usa apenas
    /// <c>conta.Email</c> e <c>conta.TipoConta</c>; o <c>Id</c> auto-gerado e
    /// passado pra <c>alunoRepository.ObterPorContaIdAsync(It.IsAny)</c>, que
    /// retorna o aluno fixo. Sem reflection necessario.
    /// </summary>
    private static IContaRepository BuildContaRepositoryMock()
    {
        var conta = Conta.Criar(
            Email.Criar("arthur@forzion.tech"),
            "pact-test-hash",
            TipoConta.Aluno,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        var mock = new Mock<IContaRepository>();
        mock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        return mock.Object;
    }

    /// <summary>
    /// Aluno: cobre listagem admin (<c>ListarTodosAsync</c>) E lookup por conta
    /// (<c>ObterPorContaIdAsync</c>, usado pelo handler de perfil).
    /// </summary>
    private static IAlunoRepository BuildAlunoRepositoryMock()
    {
        var aluno = Aluno.Criar(
            TestContaId,
            "Arthur Webster",
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            email: "arthur@forzion.tech");

        var alunoAdmin = Aluno.Criar(
            Guid.NewGuid(),
            "Joao Silva",
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            email: "joao@exemplo.com");

        var mock = new Mock<IAlunoRepository>();
        IReadOnlyList<Aluno> lista = new[] { alunoAdmin };
        mock.Setup(r => r.ListarTodosAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<AlunoStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((lista, 1));
        mock.Setup(r => r.ObterPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);
        mock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);
        return mock.Object;
    }

    /// <summary>Treinador: usado por <c>ObterVinculoAlunoHandler</c> via <c>ObterPorIdAsync</c>.</summary>
    private static ITreinadorRepository BuildTreinadorRepositoryMock()
    {
        var treinador = Treinador.Criar(
            Guid.NewGuid(),
            "Coach Silva",
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        var mock = new Mock<ITreinadorRepository>();
        mock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        mock.Setup(r => r.ObterPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        return mock.Object;
    }

    /// <summary>
    /// Vinculo ativo (apos <c>Aprovar</c>) + pendente null. O handler le
    /// <c>DataInicio</c>/<c>CreatedAt</c> direto da entidade — contrato valida shape,
    /// nao valores literais (MatchersV3 string()).
    /// </summary>
    private static IVinculoTreinadorAlunoRepository BuildVinculoRepositoryMock()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, alunoId,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        vinculo.Aprovar(Guid.NewGuid(), Guid.NewGuid());

        var mock = new Mock<IVinculoTreinadorAlunoRepository>();
        mock.Setup(r => r.ObterAtivoPorAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);
        mock.Setup(r => r.ObterPendentePorAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);
        return mock.Object;
    }

    /// <summary>Ficha do aluno (1 treino sem exercicios) — handler agrega + serializa.</summary>
    private static ITreinoAlunoRepository BuildTreinoAlunoRepositoryMock()
    {
        var treino = Treino.Criar(
            "Treino A - Superiores",
            ObjetivoTreino.Hipertrofia,
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        var treinoAluno = TreinoAluno.Criar(treino.Id, Guid.NewGuid(),
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        var detalhe = new TreinoAlunoDetalhe(treinoAluno, treino);

        var mock = new Mock<ITreinoAlunoRepository>();
        IReadOnlyList<TreinoAlunoDetalhe> lista = new[] { detalhe };
        mock.Setup(r => r.ListarDetalhesPorAlunoAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((lista, 1));
        return mock.Object;
    }

    /// <summary>Map nome-por-exercicio vazio — Treino do mock nao tem exercicios.</summary>
    private static IExercicioRepository BuildExercicioRepositoryMock()
    {
        var mock = new Mock<IExercicioRepository>();
        mock.Setup(r => r.ObterNomesPorIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        return mock.Object;
    }

    private static IUserContext BuildUserContextMock()
    {
        var userContext = new Mock<IUserContext>();
        userContext.Setup(u => u.ContaId).Returns(TestContaId);
        userContext.Setup(u => u.PerfilId).Returns(Guid.NewGuid());
        userContext.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
        return userContext.Object;
    }
}

/// <summary>
/// F5b — IStartupFilter que insere o ProviderStateMiddleware ANTES do pipeline
/// padrao. Necessario pra (a) servir POST /_pact/provider-states e (b)
/// interceptar requests com state casando cenario de erro (401/404/500) antes
/// que cheguem a auth/endpoints reais.
/// </summary>
public class ProviderStateStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.UseMiddleware<ProviderStateMiddleware>();
            next(app);
        };
}

/// <summary>
/// F5b — Middleware do provider state Pact.
///
/// Comportamento:
///   POST /_pact/provider-states  → le `{ state }` do JSON body, seta singleton, 200.
///   Demais requests             → consulta o state atual e, se casar um cenario
///                                  de erro, escreve ProblemDetails imediato.
///                                  Caso contrario, deixa o pipeline rodar (happy
///                                  path 200 via repos mockados).
///
/// Mapping de state → status:
///   "requisicao sem credenciais validas"      → 401
///   "requisicao sem perfil de admin"          → 401
///   "aluno autenticado mas nao existe no banco" → 404
///   "aluno autenticado mas sem registro"      → 404
///   "conta autenticada mas sem perfil"        → 404
///   "admin autenticado em recurso ausente"    → 404
///   "falha inesperada no backend"             → 500
///   default / null                            → no-op (happy path)
/// </summary>
public class ProviderStateMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ForzionApiProviderFactory.ProviderStateContext _state;

    public ProviderStateMiddleware(RequestDelegate next, ForzionApiProviderFactory.ProviderStateContext state)
    {
        _next = next;
        _state = state;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Endpoint que broker bate antes de cada interacao.
        if (context.Request.Path.Equals("/_pact/provider-states", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync().ConfigureAwait(false);
            string? newState = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("state", out var stateProp))
                        newState = stateProp.GetString();
                }
                catch (System.Text.Json.JsonException) { /* state vazio == reset */ }
            }
            _state.Current = newState;
            context.Response.StatusCode = 200;
            return;
        }

        // Verifica se o state atual mapeia pra erro.
        var (status, title) = MapStateToError(_state.Current);
        if (status > 0)
        {
            await WriteProblemDetailsAsync(context, status, title).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static (int Status, string Title) MapStateToError(string? state)
    {
        if (string.IsNullOrEmpty(state)) return (0, "");
        var lower = state.ToLowerInvariant();

        if (lower.Contains("sem credenciais") || lower.Contains("sem perfil de admin"))
            return (401, "Não autorizado");
        if (lower.Contains("nao existe") || lower.Contains("sem registro") ||
            lower.Contains("sem perfil") || lower.Contains("recurso ausente"))
            return (404, "Não encontrado");
        if (lower.Contains("falha inesperada"))
            return (500, "Erro interno");
        return (0, "");
    }

    private static async Task WriteProblemDetailsAsync(HttpContext context, int status, string title)
    {
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = "erro representativo",
            Instance = context.Request.Path,
        };
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(problem).ConfigureAwait(false);
    }
}

/// <summary>
/// Auth de teste: autentica toda requisicao com o `tipo_conta` derivado do path
/// (os contratos do consumer nao mandam Authorization). Satisfaz as policies
/// "Aluno"/"SystemAdmin" e o `/conta` (apenas autenticado).
/// </summary>
public class PathRoleAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public PathRoleAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var path = Request.Path.Value ?? string.Empty;
        var tipoConta = path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) ? "SystemAdmin"
            : path.StartsWith("/treinador", StringComparison.OrdinalIgnoreCase) ? "Treinador"
            : "Aluno";

        var claims = new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("tipo_conta", tipoConta),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
    }
}
