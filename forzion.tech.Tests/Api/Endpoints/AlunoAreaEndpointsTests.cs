using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.ListarExecucoesAluno;
using forzion.tech.Application.UseCases.Alunos.ListarFichasAluno;
using forzion.tech.Application.UseCases.Alunos.ObterFichaAluno;
using forzion.tech.Application.UseCases.Alunos.ObterMinhaProgressao;
using forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;
using forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;
using forzion.tech.Application.UseCases.Vinculos;
using forzion.tech.Application.UseCases.Vinculos.ObterVinculoAluno;
using forzion.tech.Application.UseCases.Vinculos.SolicitarTrocaTreinador;
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

public class AlunoAreaEndpointsTests : IClassFixture<AlunoAreaEndpointsTests.AlunoAreaWebFactory>
{
    private readonly AlunoAreaWebFactory _factory;

    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid ContaId = Guid.NewGuid();
    private static readonly Guid TreinoAlunoId = Guid.NewGuid();
    private static readonly Guid TreinoId = Guid.NewGuid();
    private static readonly Guid NovoTreinadorId = Guid.NewGuid();

    private static readonly ObterVinculoAlunoResponse RespostaVinculo = new(null, null);

    private static readonly VinculoResponse RespostaNovoVinculo = new(
        Guid.NewGuid(), NovoTreinadorId, AlunoId, Guid.NewGuid(), VinculoStatus.AguardandoAprovacao, DateTime.UtcNow);

    private static readonly ListarFichasAlunoResponse RespostaFichas = new([], 0, 1, 20);

    private static readonly FichaAlunoDetalheResponse RespostaFichaDetalhe = new(
        TreinoAlunoId, TreinoId, "Treino A", ObjetivoTreino.Hipertrofia, "Ativo", []);

    private static readonly ListarExecucoesAlunoResponse RespostaExecucoes = new([], 0, 1, 20);

    private static readonly ProgressaoAlunoResponse RespostaProgressao = new([]);

    private static readonly RegistrarExecucaoResponse RespostaExecucao = new(
        Guid.NewGuid(), TreinoId, AlunoId, DateTime.UtcNow, null, DateTime.UtcNow);

    public AlunoAreaEndpointsTests(AlunoAreaWebFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CriarClienteAluno()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "aluno");
        return client;
    }

    private HttpClient CriarClienteTreinador()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "treinador");
        return client;
    }

    // --- Auth ---

    [Fact]
    public async Task Get_Vinculo_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/aluno/vinculo");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Vinculo_RoleErrada_Retorna403()
    {
        var response = await CriarClienteTreinador().GetAsync("/aluno/vinculo");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- GET /aluno/vinculo ---

    [Fact]
    public async Task Get_Vinculo_Aluno_Retorna200()
    {
        _factory.ObterVinculoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaVinculo);

        var response = await CriarClienteAluno().GetAsync("/aluno/vinculo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /aluno/troca-treinador ---

    [Fact]
    public async Task Post_TrocaTreinador_Aluno_Retorna201()
    {
        _factory.SolicitarTrocaHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<SolicitarTrocaTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaNovoVinculo);

        var response = await CriarClienteAluno().PostAsJsonAsync("/aluno/troca-treinador",
            new { NovoTreinadorId, PacoteId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- GET /aluno/fichas ---

    [Fact]
    public async Task Get_Fichas_Aluno_Retorna200()
    {
        _factory.ListarFichasHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaFichas);

        var response = await CriarClienteAluno().GetAsync("/aluno/fichas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /aluno/fichas/{id} ---

    [Fact]
    public async Task Get_FichaDetalhe_Aluno_Retorna200()
    {
        _factory.ObterFichaHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaFichaDetalhe);

        var response = await CriarClienteAluno().GetAsync($"/aluno/fichas/{TreinoAlunoId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GET /aluno/execucoes ---

    [Fact]
    public async Task Get_Execucoes_Aluno_Retorna200()
    {
        _factory.ListarExecucoesHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaExecucoes);

        var response = await CriarClienteAluno().GetAsync("/aluno/execucoes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /aluno/execucoes ---

    [Fact]
    public async Task Post_RegistrarExecucao_Aluno_Retorna201()
    {
        _factory.RegistrarExecucaoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<RegistrarExecucaoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaExecucao);

        var response = await CriarClienteAluno().PostAsJsonAsync("/aluno/execucoes",
            new
            {
                TreinoId,
                DataExecucao = DateTime.UtcNow,
                Observacao = (string?)null,
                Exercicios = Array.Empty<object>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- GET /aluno/progressao ---

    [Fact]
    public async Task Get_Progressao_Aluno_Retorna200()
    {
        _factory.ObterProgressaoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaProgressao);

        var response = await CriarClienteAluno().GetAsync("/aluno/progressao");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- WebApplicationFactory ---

    public class AlunoAreaWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ObterVinculoAlunoHandler> ObterVinculoHandlerMock { get; } = new(
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<ITreinadorRepository>());

        public Mock<SolicitarTrocaTreinadorHandler> SolicitarTrocaHandlerMock { get; } = new(
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserContext>(),
            Mock.Of<ILogger<SolicitarTrocaTreinadorHandler>>());

        public Mock<ListarFichasAlunoHandler> ListarFichasHandlerMock { get; } = new(
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IExercicioRepository>());

        public Mock<ObterFichaAlunoHandler> ObterFichaHandlerMock { get; } = new(
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IExercicioRepository>(),
            Mock.Of<IUserContext>());

        public Mock<ListarExecucoesAlunoHandler> ListarExecucoesHandlerMock { get; } = new(
            Mock.Of<IExecucaoTreinoRepository>(),
            Mock.Of<IUserContext>());

        public Mock<ObterMinhaProgressaoHandler> ObterProgressaoHandlerMock { get; } = new(
            Mock.Of<IExecucaoTreinoRepository>(),
            Mock.Of<IUserContext>());

        public Mock<RegistrarExecucaoHandler> RegistrarExecucaoHandlerMock { get; } = new(
            Mock.Of<ITreinoRepository>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<ITreinoAlunoRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(),
            Mock.Of<IExecucaoTreinoRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserContext>(),
            Mock.Of<ILogger<RegistrarExecucaoHandler>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ObterVinculoAlunoHandler>();
                services.RemoveAll<SolicitarTrocaTreinadorHandler>();
                services.RemoveAll<ListarFichasAlunoHandler>();
                services.RemoveAll<ObterFichaAlunoHandler>();
                services.RemoveAll<ListarExecucoesAlunoHandler>();
                services.RemoveAll<ObterMinhaProgressaoHandler>();
                services.RemoveAll<RegistrarExecucaoHandler>();
                services.RemoveAll<IUserContext>();

                services.AddScoped(_ => ObterVinculoHandlerMock.Object);
                services.AddScoped(_ => SolicitarTrocaHandlerMock.Object);
                services.AddScoped(_ => ListarFichasHandlerMock.Object);
                services.AddScoped(_ => ObterFichaHandlerMock.Object);
                services.AddScoped(_ => ListarExecucoesHandlerMock.Object);
                services.AddScoped(_ => ObterProgressaoHandlerMock.Object);
                services.AddScoped(_ => RegistrarExecucaoHandlerMock.Object);

                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.ContaId).Returns(ContaId);
                userContextMock.Setup(u => u.PerfilId).Returns(AlunoId);
                userContextMock.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
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

            var param = header.Replace("Test ", "");

            string tipoConta;
            string userId;

            if (param == "aluno")
            {
                tipoConta = "Aluno";
                userId = AlunoId.ToString();
            }
            else if (param == "treinador")
            {
                tipoConta = "Treinador";
                userId = Guid.NewGuid().ToString();
            }
            else
            {
                return Task.FromResult(AuthenticateResult.Fail("Token inválido"));
            }

            var claims = new[]
            {
                new Claim("sub", userId),
                new Claim("tipo_conta", tipoConta),
                new Claim("perfil_id", AlunoId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
