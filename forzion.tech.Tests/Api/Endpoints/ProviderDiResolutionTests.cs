using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

/// <summary>
/// Smoke de boot/DI gated no PR (este projeto entra na slnx; o projeto Pact nao).
/// Mocka os repositorios no nivel REPO (handlers reais executam) como a factory do
/// Pact, e bate nos 4 endpoints VERIFICADOS pelo contrato — assert status != 5xx.
///
/// Valor (FR-4): um repo novo injetado num handler de endpoint verificado SEM ser
/// mockado aqui cai na impl real (precisa de DbContext, ausente em env Test) -> 5xx
/// -> este teste falha NO PR. Deliberadamente NAO ha auto-mock fallback (isso e do
/// caminho Pact/FR-5); aqui o nao-mock e justamente o detector.
/// </summary>
public class ProviderDiResolutionTests : IClassFixture<ProviderDiResolutionTests.ProviderDiWebFactory>
{
    private readonly ProviderDiWebFactory _factory;

    public ProviderDiResolutionTests(ProviderDiWebFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/conta/perfil")]
    [InlineData("/aluno/vinculo")]
    [InlineData("/aluno/fichas")]
    [InlineData("/admin/alunos")]
    public async Task EndpointVerificado_HappyPath_NaoRetorna5xx(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        ((int)response.StatusCode).Should().BeLessThan(500,
            $"endpoint verificado {path} deve resolver o grafo de DI e responder sem 5xx; " +
            "5xx indica dep nova nao mockada (impl real sem DbContext) ou quebra de boot");
    }

    public class ProviderDiWebFactory : WebApplicationFactory<Program>
    {
        // ContaId fixo: IUserContext + IContaRepository.ObterPorIdAsync precisam casar
        // o mesmo id que o handler de perfil consulta.
        private static readonly Guid TestContaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly DateTime Agora = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAlunoRepository>();
                services.RemoveAll<IContaRepository>();
                services.RemoveAll<ITreinadorRepository>();
                services.RemoveAll<IVinculoTreinadorAlunoRepository>();
                services.RemoveAll<ITreinoAlunoRepository>();
                services.RemoveAll<IExercicioRepository>();
                services.RemoveAll<ISystemUserRepository>();
                services.RemoveAll<IUserContext>();

                services.AddScoped(_ => BuildAlunoRepositoryMock());
                services.AddScoped(_ => BuildContaRepositoryMock());
                services.AddScoped(_ => BuildTreinadorRepositoryMock());
                services.AddScoped(_ => BuildVinculoRepositoryMock());
                services.AddScoped(_ => BuildTreinoAlunoRepositoryMock());
                services.AddScoped(_ => BuildExercicioRepositoryMock());
                services.AddScoped(_ => BuildSystemUserRepositoryMock());
                services.AddScoped(_ => BuildUserContextMock());

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, PathRoleAuthHandler>("Test", _ => { });
            });
        }

        private static IContaRepository BuildContaRepositoryMock()
        {
            var conta = Conta.Criar(
                Email.Criar("arthur@forzion.tech").Value,
                "pact-test-hash",
                TipoConta.Aluno,
                Agora).Value;

            var mock = new Mock<IContaRepository>();
            mock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(conta);
            return mock.Object;
        }

        private static IAlunoRepository BuildAlunoRepositoryMock()
        {
            var aluno = Aluno.Criar(TestContaId, "Arthur Webster", Agora, email: "arthur@forzion.tech").Value;
            var alunoAdmin = Aluno.Criar(Guid.NewGuid(), "Joao Silva", Agora, email: "joao@exemplo.com").Value;

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

        private static ITreinadorRepository BuildTreinadorRepositoryMock()
        {
            var treinador = Treinador.Criar(Guid.NewGuid(), "Coach Silva", Agora).Value;

            var mock = new Mock<ITreinadorRepository>();
            mock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(treinador);
            mock.Setup(r => r.ObterPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(treinador);
            return mock.Object;
        }

        private static IVinculoTreinadorAlunoRepository BuildVinculoRepositoryMock()
        {
            var vinculo = VinculoTreinadorAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Agora).Value;
            vinculo.Aprovar(Guid.NewGuid(), Guid.NewGuid(), Agora);

            var mock = new Mock<IVinculoTreinadorAlunoRepository>();
            mock.Setup(r => r.ObterAtivoPorAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(vinculo);
            mock.Setup(r => r.ObterPendentePorAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((VinculoTreinadorAluno?)null);
            return mock.Object;
        }

        private static ITreinoAlunoRepository BuildTreinoAlunoRepositoryMock()
        {
            var treino = Treino.Criar("Treino A - Superiores", ObjetivoTreino.Hipertrofia, Guid.NewGuid(), Agora).Value;
            var treinoAluno = TreinoAluno.Criar(treino.Id, Guid.NewGuid(), Agora).Value;
            var detalhe = new TreinoAlunoDetalhe(treinoAluno, treino);

            var mock = new Mock<ITreinoAlunoRepository>();
            IReadOnlyList<TreinoAlunoDetalhe> lista = new[] { detalhe };
            mock.Setup(r => r.ListarDetalhesPorAlunoAsync(
                    It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((lista, 1));
            return mock.Object;
        }

        private static IExercicioRepository BuildExercicioRepositoryMock()
        {
            var mock = new Mock<IExercicioRepository>();
            mock.Setup(r => r.ObterNomesPorIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string>());
            return mock.Object;
        }

        private static ISystemUserRepository BuildSystemUserRepositoryMock()
        {
            var systemUser = SystemUser.Criar(Guid.NewGuid(), "Admin Forzion", Agora).Value;

            var mock = new Mock<ISystemUserRepository>();
            mock.Setup(r => r.ObterPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(systemUser);
            mock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(systemUser);
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

    /// <summary>Autentica toda requisicao com tipo_conta derivado do path (satisfaz as
    /// policies Aluno/SystemAdmin e o /conta apenas-autenticado).</summary>
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
}
