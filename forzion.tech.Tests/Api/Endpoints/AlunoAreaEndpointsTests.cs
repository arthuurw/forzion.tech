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
using forzion.tech.Application.UseCases.AssinaturaAlunos;
using forzion.tech.Application.UseCases.AssinaturaAlunos.ObterAssinaturaAluno;
using forzion.tech.Application.UseCases.Pagamentos;
using forzion.tech.Application.UseCases.Pagamentos.ListarPagamentosAssinaturaAluno;
using forzion.tech.Application.UseCases.Pagamentos.ObterStatusPagamento;
using forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;
using forzion.tech.Application.UseCases.Vinculos;
using forzion.tech.Application.UseCases.Vinculos.ObterVinculoAluno;
using forzion.tech.Application.UseCases.Vinculos.SolicitarTrocaTreinador;
using forzion.tech.Domain.Shared;
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

    private static readonly Guid PagamentoId = Guid.NewGuid();
    private static readonly Guid AssinaturaAlunoId = Guid.NewGuid();

    private static readonly AssinaturaAlunoResponse RespostaAssinaturaAluno = new(
        AssinaturaAlunoId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AlunoId,
        99.90m, AssinaturaAlunoStatus.Ativa, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), null, DateTime.UtcNow);

    private static readonly PagamentoResponse RespostaPagamento = new(
        PagamentoId, AssinaturaAlunoId, 99.90m, PagamentoStatus.Pendente, MetodoPagamento.Pix,
        "qrcode", "https://example.com/qr", DateTime.UtcNow.AddHours(1), null, null, DateTime.UtcNow);

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
            .ReturnsAsync(Result.Success(RespostaNovoVinculo));

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
            .ReturnsAsync(Result.Success(RespostaExecucao));

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

    // --- GET /aluno/assinatura ---

    [Fact]
    public async Task Get_AssinaturaAluno_Aluno_Retorna200()
    {
        _factory.ObterAssinaturaAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaAssinaturaAluno);

        var response = await CriarClienteAluno().GetAsync("/aluno/assinatura");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_AssinaturaAluno_SemAssinaturaAluno_Retorna204()
    {
        _factory.ObterAssinaturaAlunoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAlunoResponse?)null);

        var response = await CriarClienteAluno().GetAsync("/aluno/assinatura");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // --- GET /aluno/pagamentos/{id} ---

    [Fact]
    public async Task Get_Pagamento_Aluno_Retorna200()
    {
        _factory.ObterStatusPagamentoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterStatusPagamentoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespostaPagamento);

        var response = await CriarClienteAluno().GetAsync($"/aluno/pagamentos/{PagamentoId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_Pagamento_AcessoNegado_Retorna403()
    {
        _factory.ObterStatusPagamentoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterStatusPagamentoQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AcessoNegadoException());

        var response = await CriarClienteAluno().GetAsync($"/aluno/pagamentos/{PagamentoId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- GET /aluno/pagamentos/assinatura/{id} ---

    [Fact]
    public async Task Get_ListarPagamentosAssinaturaAluno_Aluno_Retorna200()
    {
        _factory.ListarPagamentosHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ListarPagamentosAssinaturaAlunoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PagamentoResponse> { RespostaPagamento });

        var response = await CriarClienteAluno().GetAsync($"/aluno/pagamentos/assinatura/{AssinaturaAlunoId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /aluno/troca-treinador error paths ---

    [Fact]
    public async Task Post_TrocaTreinador_NaoEncontrado_Retorna404()
    {
        _factory.SolicitarTrocaHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<SolicitarTrocaTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TreinadorNaoEncontradoException());

        var response = await CriarClienteAluno().PostAsJsonAsync("/aluno/troca-treinador",
            new { NovoTreinadorId, PacoteId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_TrocaTreinador_DomainException_Retorna422()
    {
        _factory.SolicitarTrocaHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<SolicitarTrocaTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<VinculoResponse>(Error.Business("Aluno já possui vínculo ativo.")));

        var response = await CriarClienteAluno().PostAsJsonAsync("/aluno/troca-treinador",
            new { NovoTreinadorId, PacoteId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- GET /aluno/progressao date validation ---

    [Fact]
    public async Task Get_Progressao_DataInvalida_Retorna400()
    {
        var ontem = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var hoje = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var response = await CriarClienteAluno()
            .GetAsync($"/aluno/progressao?de={hoje}&ate={ontem}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    // --- GET /aluno/pagamentos/assinatura/{id} error path ---

    [Fact]
    public async Task Get_PagamentosAssinaturaAluno_AcessoNegado_Retorna403()
    {
        _factory.ListarPagamentosHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ListarPagamentosAssinaturaAlunoQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AcessoNegadoException());

        var response = await CriarClienteAluno().GetAsync($"/aluno/pagamentos/assinatura/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- GET /aluno/fichas/{id} error path ---

    [Fact]
    public async Task Get_FichaDetalhe_NaoEncontrado_Retorna404()
    {
        _factory.ObterFichaHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TreinoNaoEncontradoException());

        var response = await CriarClienteAluno().GetAsync($"/aluno/fichas/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- POST /aluno/execucoes error path ---

    [Fact]
    public async Task Post_RegistrarExecucao_DomainException_Retorna422()
    {
        _factory.RegistrarExecucaoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<RegistrarExecucaoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<RegistrarExecucaoResponse>(Error.Business("Treino não disponível.")));

        var response = await CriarClienteAluno().PostAsJsonAsync("/aluno/execucoes",
            new
            {
                TreinoId,
                DataExecucao = DateTime.UtcNow,
                Observacao = (string?)null,
                Exercicios = Array.Empty<object>()
            });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
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
            Mock.Of<IUserContext>(), TimeProvider.System,
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
            Mock.Of<IUserContext>(), TimeProvider.System,
            Mock.Of<ILogger<RegistrarExecucaoHandler>>());

        public Mock<ObterAssinaturaAlunoHandler> ObterAssinaturaAlunoHandlerMock { get; } = new(
            Mock.Of<IAssinaturaAlunoRepository>());

        public Mock<ObterStatusPagamentoHandler> ObterStatusPagamentoHandlerMock { get; } = new(
            Mock.Of<IPagamentoRepository>(),
            Mock.Of<IAssinaturaAlunoRepository>());

        public Mock<ListarPagamentosAssinaturaAlunoHandler> ListarPagamentosHandlerMock { get; } = new(
            Mock.Of<IPagamentoRepository>(),
            Mock.Of<IAssinaturaAlunoRepository>());

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
                services.RemoveAll<ObterAssinaturaAlunoHandler>();
                services.RemoveAll<ObterStatusPagamentoHandler>();
                services.RemoveAll<ListarPagamentosAssinaturaAlunoHandler>();
                services.RemoveAll<IUserContext>();

                services.AddScoped(_ => ObterVinculoHandlerMock.Object);
                services.AddScoped(_ => SolicitarTrocaHandlerMock.Object);
                services.AddScoped(_ => ListarFichasHandlerMock.Object);
                services.AddScoped(_ => ObterFichaHandlerMock.Object);
                services.AddScoped(_ => ListarExecucoesHandlerMock.Object);
                services.AddScoped(_ => ObterProgressaoHandlerMock.Object);
                services.AddScoped(_ => RegistrarExecucaoHandlerMock.Object);
                services.AddScoped(_ => ObterAssinaturaAlunoHandlerMock.Object);
                services.AddScoped(_ => ObterStatusPagamentoHandlerMock.Object);
                services.AddScoped(_ => ListarPagamentosHandlerMock.Object);

                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.ContaId).Returns(ContaId);
                userContextMock.Setup(u => u.PerfilId).Returns(AlunoId);
                userContextMock.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
                services.AddScoped(_ => userContextMock.Object);

                // Repositórios usados pelo RequireAssinaturaAtivaFilter — stubs sem
                // aluno cadastrado fazem o filtro entrar em bypass, mantendo
                // os testes existentes de POST /aluno/execucoes funcionando.
                services.RemoveAll<IAlunoRepository>();
                services.RemoveAll<IAssinaturaAlunoRepository>();
                services.AddScoped(_ => Mock.Of<IAlunoRepository>());
                services.AddScoped(_ => Mock.Of<IAssinaturaAlunoRepository>());

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
