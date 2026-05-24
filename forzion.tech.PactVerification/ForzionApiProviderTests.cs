using System.Security.Claims;
using System.Text.Encodings.Web;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.Alunos.ListarAlunosAdmin;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;
using forzion.tech.Application.UseCases.Alunos.ListarFichasAluno;
using forzion.tech.Application.UseCases.Conta.ObterPerfil;
using forzion.tech.Application.UseCases.Treinos;
using forzion.tech.Application.UseCases.Vinculos.ObterVinculoAluno;
using forzion.tech.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
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
            .Verify();
    }
}

/// <summary>
/// Hospeda a API com os handlers dos contratos mockados (retornam shapes
/// representativos) e auth de teste por path. Sem DB: Program sobe em env Test
/// sem conexao e os handlers mockados nao tocam repositorios.
///
/// O verifier nativo do Pact faz HTTP real, entao TestServer (in-memory) nao
/// serve: o truque de 2 hosts inicia um Kestrel numa porta livre.
/// </summary>
public class ForzionApiProviderFactory : WebApplicationFactory<Program>
{
    private IHost? _kestrelHost;

    public string ServerUri { get; private set; } = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("AllowedHosts", "*");
        builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ListarFichasAlunoHandler>();
            services.RemoveAll<ObterVinculoAlunoHandler>();
            services.RemoveAll<ObterPerfilHandler>();
            services.RemoveAll<ListarAlunosAdminHandler>();
            services.RemoveAll<IUserContext>();

            services.AddScoped(_ => BuildFichasHandler());
            services.AddScoped(_ => BuildVinculoHandler());
            services.AddScoped(_ => BuildPerfilHandler());
            services.AddScoped(_ => BuildListarAlunosHandler());

            var userContext = new Mock<IUserContext>();
            userContext.Setup(u => u.ContaId).Returns(Guid.NewGuid());
            userContext.Setup(u => u.PerfilId).Returns(Guid.NewGuid());
            userContext.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
            services.AddScoped(_ => userContext.Object);

            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, PathRoleAuthHandler>("Test", _ => { });
        });
    }

    // Truque dos 2 hosts: base.CreateHost sobe o TestServer (exigido pelo WAF);
    // depois reusamos o mesmo builder (com os overrides acima) pra subir um
    // Kestrel real numa porta livre, que o verifier do Pact consome via HTTP.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = base.CreateHost(builder);

        builder.ConfigureWebHost(b => b.UseKestrel(o => o.ListenLocalhost(0)));
        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        var addresses = _kestrelHost.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>();
        ServerUri = addresses!.Addresses.First();

        return testHost;
    }

    protected override void Dispose(bool disposing)
    {
        _kestrelHost?.Dispose();
        base.Dispose(disposing);
    }

    private static ListarFichasAlunoHandler BuildFichasHandler()
    {
        var mock = new Mock<ListarFichasAlunoHandler>(
            Mock.Of<ITreinoAlunoRepository>(), Mock.Of<IExercicioRepository>());

        var resposta = new ListarFichasAlunoResponse(
            new[]
            {
                new FichaAlunoResponse(
                    Guid.NewGuid(), Guid.NewGuid(), "Treino A - Superiores",
                    ObjetivoTreino.Hipertrofia, TreinoAlunoStatus.Ativo,
                    Array.Empty<TreinoExercicioResponse>()),
            },
            Total: 1, Pagina: 1, TamanhoPagina: 20);

        mock.Setup(h => h.HandleAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resposta);
        return mock.Object;
    }

    private static ObterVinculoAlunoHandler BuildVinculoHandler()
    {
        var mock = new Mock<ObterVinculoAlunoHandler>(
            Mock.Of<IVinculoTreinadorAlunoRepository>(), Mock.Of<ITreinadorRepository>());

        var resposta = new ObterVinculoAlunoResponse(
            new VinculoAlunoItemResponse(
                Guid.NewGuid(), Guid.NewGuid(), "Coach Silva",
                VinculoStatus.Ativo,
                new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)),
            VinculoPendente: null);

        mock.Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resposta);
        return mock.Object;
    }

    private static ObterPerfilHandler BuildPerfilHandler()
    {
        var mock = new Mock<ObterPerfilHandler>(
            Mock.Of<IUserContext>(), Mock.Of<IContaRepository>(), Mock.Of<IAlunoRepository>(),
            Mock.Of<ITreinadorRepository>(), Mock.Of<ISystemUserRepository>());

        mock.Setup(h => h.HandleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PerfilResponse("Arthur Webster", "arthur@forzion.tech", "Aluno"));
        return mock.Object;
    }

    private static ListarAlunosAdminHandler BuildListarAlunosHandler()
    {
        var mock = new Mock<ListarAlunosAdminHandler>(Mock.Of<IAlunoRepository>());

        var resposta = new ListarAlunosResponse(
            new[]
            {
                new AlunoResponse(
                    Guid.NewGuid(), "Joao Silva", "joao@exemplo.com", null,
                    AlunoStatus.Ativo, Guid.NewGuid(),
                    new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), null),
            },
            Total: 1, Pagina: 1, TamanhoPagina: 20);

        mock.Setup(h => h.HandleAsync(It.IsAny<ListarAlunosAdminQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resposta);
        return mock.Object;
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
