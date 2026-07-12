using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Application.Settings;
using forzion.tech.Application.UseCases.Pagamentos;
using forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;
using forzion.tech.Application.UseCases.Pagamentos.ListarRecebimentosTreinador;
using forzion.tech.Application.UseCases.Pagamentos.ObterStatusPagamento;
using forzion.tech.Application.UseCases.Pagamentos.ReconciliarPagamentosStripe;
using forzion.tech.Application.UseCases.Treinadores.ContratarPlanoTreinador;
using forzion.tech.Application.UseCases.Treinadores.GerarCobrancaPlanoTreinador;
using forzion.tech.Application.UseCases.Treinadores.IniciarPagamentoPlano;
using forzion.tech.Application.UseCases.Treinadores.TrocarPlanoTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

public class PagamentosEndpointsTests
    : IClassFixture<PagamentosEndpointsTests.PagamentosWebFactory>,
      IClassFixture<PagamentosEndpointsTests.PagamentosMutableFactory>
{
    private const string ChaveInterna = "chave-interna-de-teste";
    private static readonly Guid ContaId = Guid.NewGuid();
    private static readonly Guid PerfilId = Guid.NewGuid();
    private readonly PagamentosWebFactory _factory;
    private readonly PagamentosMutableFactory _mutable;

    public PagamentosEndpointsTests(PagamentosWebFactory factory, PagamentosMutableFactory mutable)
    {
        _factory = factory;
        _mutable = mutable;
    }

    private HttpClient ClienteTreinador() => ClienteTreinador(_factory);

    private static HttpClient ClienteTreinador(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", ContaId.ToString());
        return client;
    }

    private HttpClient ClienteAluno() => ClienteAluno(_factory);

    private static HttpClient ClienteAluno(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("TestAluno", ContaId.ToString());
        return client;
    }

    [Fact]
    public async Task Post_CobrarPlano_MetodoForaDoEnum_Retorna400()
    {
        var response = await ClienteTreinador().PostAsync("/treinador/plano/cobrar?metodo=999", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_CobrarAssinatura_MetodoForaDoEnum_Retorna400()
    {
        var response = await ClienteTreinador()
            .PostAsync($"/treinador/pagamentos/cobrar/{Guid.NewGuid()}?metodo=999", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static ListarRecebimentosTreinadorQuery? CapturedRecebimentosQuery;

    [Fact]
    public async Task Get_Recebimentos_EscopaNoTreinadorAutenticado()
    {
        CapturedRecebimentosQuery = null;

        var response = await ClienteTreinador()
            .GetAsync("/treinador/pagamentos/recebimentos?tamanho=5&cursor=abc");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CapturedRecebimentosQuery.Should().NotBeNull();
        CapturedRecebimentosQuery!.TreinadorId.Should().Be(PerfilId);
        CapturedRecebimentosQuery.Tamanho.Should().Be(5);
        CapturedRecebimentosQuery.Cursor.Should().Be("abc");
    }

    [Fact]
    public async Task Get_AssinaturaTreinador_SemAssinatura_Retorna200ComCorpoNulo()
    {
        var response = await ClienteTreinador().GetAsync("/treinador/plano/assinatura");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Trim().Should().Be("null");
    }

    [Fact]
    public async Task Get_Recebimentos_SemToken_Retorna401()
    {
        var response = await _factory.CreateClient()
            .GetAsync("/treinador/pagamentos/recebimentos");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_StatusPagamento_ComAlunoAutenticado_NaoRetorna401()
    {
        var response = await ClienteAluno().GetAsync($"/aluno/pagamentos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_StatusPagamento_HandlerSucesso_Retorna200()
    {
        var payload = new PagamentoResponse(
            Guid.NewGuid(), Guid.NewGuid(), 149.90m, PagamentoStatus.Pendente, MetodoPagamento.Pix,
            null, null, null, null, null, DateTime.UtcNow);
        _mutable.ObterStatusMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterStatusPagamentoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(payload));

        var response = await ClienteAluno(_mutable).GetAsync($"/aluno/pagamentos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("pagamentoId").GetGuid().Should().Be(payload.PagamentoId);
    }

    [Fact]
    public async Task Get_StatusPagamento_HandlerFalhaValidacao_Retorna400()
    {
        _mutable.ObterStatusMock
            .Setup(h => h.HandleAsync(It.IsAny<ObterStatusPagamentoQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PagamentoResponse>(
                Error.Validation("pagamento_invalido", "Pagamento inválido.")));

        var response = await ClienteAluno(_mutable).GetAsync($"/aluno/pagamentos/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_PagamentoPlanoTreinador_DeOutroTreinador_Retorna404()
    {
        var pagamentoDeOutro = PagamentoTreinador.Criar(
            Guid.NewGuid(), Guid.NewGuid(), 100m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        _mutable.PagamentoTreinadorRepoMock
            .Setup(r => r.ObterPorIdAsync(pagamentoDeOutro.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamentoDeOutro);

        var response = await ClienteTreinador(_mutable)
            .GetAsync($"/treinador/plano/pagamento/{pagamentoDeOutro.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_PagamentoPlanoTreinador_DoProprioTreinador_Retorna200()
    {
        var pagamento = PagamentoTreinador.Criar(
            PerfilId, Guid.NewGuid(), 100m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        _mutable.PagamentoTreinadorRepoMock
            .Setup(r => r.ObterPorIdAsync(pagamento.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var response = await ClienteTreinador(_mutable)
            .GetAsync($"/treinador/plano/pagamento/{pagamento.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_TrocarPlano_HandlerFalha_Retorna404()
    {
        _mutable.TrocarPlanoMock
            .Setup(h => h.HandleAsync(It.IsAny<TrocarPlanoTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<TrocarPlanoTreinadorResponse>(
                Error.NotFound("plano_nao_encontrado", "Plano não encontrado.")));

        var response = await ClienteTreinador(_mutable).PostAsJsonAsync(
            "/treinador/plano/trocar", new { planoPlataformaId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_ContratarPlano_HandlerFalha_Retorna409()
    {
        _mutable.ContratarPlanoMock
            .Setup(h => h.HandleAsync(It.IsAny<ContratarPlanoTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ContratarPlanoTreinadorResponse>(
                Error.Conflict("assinatura_ja_ativa", "Já possui assinatura ativa.")));

        var response = await ClienteTreinador(_mutable).PostAsJsonAsync(
            "/treinador/plano/contratar", new { planoPlataformaId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_CobrarPlano_AssinaturaEncerrada_Retorna200ComMensagemDowngrade()
    {
        var assinatura = AssinaturaTreinador.Criar(PerfilId, Guid.NewGuid(), 100m, DateTime.UtcNow).Value;
        _mutable.AssinaturaTreinadorRepoMock
            .Setup(r => r.ObterAtualPorTreinadorAsync(PerfilId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _mutable.CobrarPlanoMock
            .Setup(h => h.HandleAsync(It.IsAny<GerarCobrancaPlanoTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(IniciarPagamentoPlanoResponse.Encerrada()));

        var response = await ClienteTreinador(_mutable).PostAsync("/treinador/plano/cobrar?metodo=Pix", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("mensagem").GetString().Should().Contain("Downgrade");
    }

    [Fact]
    public async Task Post_CobrarPlano_HandlerFalha_Retorna400()
    {
        var assinatura = AssinaturaTreinador.Criar(PerfilId, Guid.NewGuid(), 100m, DateTime.UtcNow).Value;
        _mutable.AssinaturaTreinadorRepoMock
            .Setup(r => r.ObterAtualPorTreinadorAsync(PerfilId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _mutable.CobrarPlanoMock
            .Setup(h => h.HandleAsync(It.IsAny<GerarCobrancaPlanoTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<IniciarPagamentoPlanoResponse>(
                Error.Validation("cobranca_invalida", "Cobrança inválida.")));

        var response = await ClienteTreinador(_mutable).PostAsync("/treinador/plano/cobrar?metodo=Pix", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_ProcessarRenovacoes_SemChaveInterna_Retorna401()
    {
        var response = await _mutable.CreateClient().PostAsync("/internal/processar-renovacoes", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ProcessarRenovacoesTreinador_SemChaveInterna_Retorna401()
    {
        var response = await _mutable.CreateClient().PostAsync("/internal/processar-renovacoes-treinador", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ReconciliarPagamentos_SemChaveInterna_Retorna401()
    {
        var response = await _mutable.CreateClient().PostAsync("/internal/reconciliar-pagamentos", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ReconciliarPagamentos_Truncado_Retorna503()
    {
        _mutable.ReconciliarMock
            .Setup(h => h.HandleAsync(It.IsAny<ReconciliarPagamentosStripeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ReconciliarPagamentosStripeResponse(
                TotalEventos: 200, Replayed: 10, JaConsistentes: 190, Erros: 0,
                DesdeUtc: DateTime.UtcNow, Truncado: true, OnboardingConfirmados: 0)));

        var req = new HttpRequestMessage(HttpMethod.Post, "/internal/reconciliar-pagamentos");
        req.Headers.Add("X-Internal-Key", ChaveInterna);
        var response = await _mutable.CreateClient().SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Post_ReconciliarPagamentos_CorpoNulo_Retorna200EDesdeUtcNulo()
    {
        ReconciliarPagamentosStripeCommand? capturado = null;
        _mutable.ReconciliarMock
            .Setup(h => h.HandleAsync(It.IsAny<ReconciliarPagamentosStripeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReconciliarPagamentosStripeCommand c, CancellationToken _) =>
            {
                capturado = c;
                return Result.Success(new ReconciliarPagamentosStripeResponse(
                    TotalEventos: 0, Replayed: 0, JaConsistentes: 0, Erros: 0,
                    DesdeUtc: DateTime.UtcNow, Truncado: false, OnboardingConfirmados: 0));
            });

        var req = new HttpRequestMessage(HttpMethod.Post, "/internal/reconciliar-pagamentos");
        req.Headers.Add("X-Internal-Key", ChaveInterna);
        var response = await _mutable.CreateClient().SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturado.Should().NotBeNull();
        capturado!.DesdeUtc.Should().BeNull();
    }

    public class PagamentosMutableFactory : WebApplicationFactory<Program>
    {
        private static readonly CriarPagamentoComIntentService CriarServiceStub = new(
            Mock.Of<IUnitOfWork>(), Mock.Of<IDbContextTransactionProvider>(),
            Mock.Of<IDatabaseErrorInspector>(), Mock.Of<IStripeService>(), TimeProvider.System,
            Mock.Of<ILogger<CriarPagamentoComIntentService>>());

        public Mock<ObterStatusPagamentoHandler> ObterStatusMock { get; } = new(
            Mock.Of<IPagamentoRepository>(), Mock.Of<IAssinaturaAlunoRepository>());

        public Mock<IPagamentoTreinadorRepository> PagamentoTreinadorRepoMock { get; } = new();

        public Mock<IAssinaturaTreinadorRepository> AssinaturaTreinadorRepoMock { get; } = new();

        public Mock<TrocarPlanoTreinadorHandler> TrocarPlanoMock { get; } = new(
            Mock.Of<ITreinadorRepository>(), Mock.Of<IAssinaturaTreinadorRepository>(),
            Mock.Of<IPlanoPlataformaRepository>(), Mock.Of<IPagamentoTreinadorRepository>(),
            Mock.Of<IStripeService>(), Mock.Of<IUnitOfWork>(), CriarServiceStub, TimeProvider.System,
            Mock.Of<ILogger<TrocarPlanoTreinadorHandler>>());

        public Mock<ContratarPlanoTreinadorHandler> ContratarPlanoMock { get; } = new(
            Mock.Of<ITreinadorRepository>(), Mock.Of<IAssinaturaTreinadorRepository>(),
            Mock.Of<IPlanoPlataformaRepository>(), Mock.Of<IPagamentoTreinadorRepository>(),
            Mock.Of<IStripeService>(), CriarServiceStub, Mock.Of<IUnitOfWork>(),
            Mock.Of<IDatabaseErrorInspector>(), TimeProvider.System,
            Mock.Of<ILogger<ContratarPlanoTreinadorHandler>>());

        public Mock<GerarCobrancaPlanoTreinadorHandler> CobrarPlanoMock { get; } = new(
            Mock.Of<IAssinaturaTreinadorRepository>(), Mock.Of<IPagamentoTreinadorRepository>(),
            Mock.Of<IPlanoPlataformaRepository>(), Mock.Of<IStripeService>(), Mock.Of<IUnitOfWork>(),
            CriarServiceStub, TimeProvider.System, Mock.Of<ILogger<GerarCobrancaPlanoTreinadorHandler>>());

        public Mock<ReconciliarPagamentosStripeHandler> ReconciliarMock { get; } = new(
            Mock.Of<IStripeService>(), Mock.Of<IServiceScopeFactory>(), TimeProvider.System,
            Mock.Of<ILogger<ReconciliarPagamentosStripeHandler>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");
            builder.UseSetting("Internal:ApiKey", ChaveInterna);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ObterStatusPagamentoHandler>();
                services.RemoveAll<IPagamentoTreinadorRepository>();
                services.RemoveAll<IAssinaturaAlunoRepository>();
                services.RemoveAll<IAssinaturaTreinadorRepository>();
                services.RemoveAll<TrocarPlanoTreinadorHandler>();
                services.RemoveAll<ContratarPlanoTreinadorHandler>();
                services.RemoveAll<GerarCobrancaPlanoTreinadorHandler>();
                services.RemoveAll<ReconciliarPagamentosStripeHandler>();

                services.AddScoped(_ => ObterStatusMock.Object);
                services.AddScoped(_ => PagamentoTreinadorRepoMock.Object);
                services.AddScoped(_ => Mock.Of<IAssinaturaAlunoRepository>());
                services.AddScoped(_ => AssinaturaTreinadorRepoMock.Object);
                services.AddScoped(_ => TrocarPlanoMock.Object);
                services.AddScoped(_ => ContratarPlanoMock.Object);
                services.AddScoped(_ => CobrarPlanoMock.Object);
                services.AddScoped(_ => ReconciliarMock.Object);

                AdicionarAutenticacaoTeste(services);
            });
        }
    }

    public class PagamentosWebFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<GerarCobrancaMensalHandler>();
                services.AddScoped(_ => new Mock<GerarCobrancaMensalHandler>(
                    Mock.Of<IAssinaturaAlunoRepository>(),
                    Mock.Of<IPagamentoRepository>(),
                    Mock.Of<IContaRecebimentoRepository>(),
                    Mock.Of<IStripeService>(),
                    null!,
                    Mock.Of<IUnitOfWork>(),
                    Options.Create(new PaymentSettings()),
                    TimeProvider.System,
                    Mock.Of<ILogger<GerarCobrancaMensalHandler>>()).Object);

                services.RemoveAll<GerarCobrancaPlanoTreinadorHandler>();
                services.AddScoped(_ => new Mock<GerarCobrancaPlanoTreinadorHandler>(
                    Mock.Of<IAssinaturaTreinadorRepository>(),
                    Mock.Of<IPagamentoTreinadorRepository>(),
                    Mock.Of<IPlanoPlataformaRepository>(),
                    Mock.Of<IStripeService>(),
                    Mock.Of<IUnitOfWork>(),
                    null!,
                    TimeProvider.System,
                    Mock.Of<ILogger<GerarCobrancaPlanoTreinadorHandler>>()).Object);

                services.RemoveAll<IAssinaturaTreinadorRepository>();
                services.AddScoped(_ => Mock.Of<IAssinaturaTreinadorRepository>());

                services.RemoveAll<ListarRecebimentosTreinadorHandler>();
                var recebimentosMock = new Mock<ListarRecebimentosTreinadorHandler>(
                    Mock.Of<IPagamentoRepository>(), Options.Create(new PaymentSettings()));
                recebimentosMock
                    .Setup(h => h.HandleAsync(It.IsAny<ListarRecebimentosTreinadorQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ListarRecebimentosTreinadorQuery q, CancellationToken _) =>
                    {
                        CapturedRecebimentosQuery = q;
                        return new ListarRecebimentosTreinadorResultado([], null, 5m);
                    });
                services.AddScoped(_ => recebimentosMock.Object);

                services.RemoveAll<ObterStatusPagamentoHandler>();
                var statusPagamentoMock = new Mock<ObterStatusPagamentoHandler>(
                    Mock.Of<IPagamentoRepository>(),
                    Mock.Of<IAssinaturaAlunoRepository>());
                statusPagamentoMock
                    .Setup(h => h.HandleAsync(It.IsAny<ObterStatusPagamentoQuery>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Result.Failure<PagamentoResponse>(
                        Error.NotFound("pagamento_nao_encontrado", "Pagamento não encontrado.")));
                services.AddScoped(_ => statusPagamentoMock.Object);

                AdicionarAutenticacaoTeste(services);
            });
        }
    }

    private static void AdicionarAutenticacaoTeste(IServiceCollection services)
    {
        services.AddAuthentication("Test")
            .AddPolicyScheme("Test", "Test", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var header = context.Request.Headers.Authorization.FirstOrDefault();
                    return header is not null && header.StartsWith("TestAluno", StringComparison.Ordinal)
                        ? "TestAluno"
                        : "TestTreinador";
                };
            })
            .AddScheme<AuthenticationSchemeOptions, TreinadorTestAuthHandler>("TestTreinador", _ => { })
            .AddScheme<AuthenticationSchemeOptions, AlunoTestAuthHandler>("TestAluno", _ => { });
    }

    public class TreinadorTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TreinadorTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (string.IsNullOrEmpty(Request.Headers.Authorization.FirstOrDefault()))
                return Task.FromResult(AuthenticateResult.Fail("Sem token"));

            var claims = new[]
            {
                new Claim("sub", ContaId.ToString()),
                new Claim("tipo_conta", "Treinador"),
                new Claim("perfil_id", PerfilId.ToString()),
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
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
            if (string.IsNullOrEmpty(Request.Headers.Authorization.FirstOrDefault()))
                return Task.FromResult(AuthenticateResult.Fail("Sem token"));

            var claims = new[]
            {
                new Claim("sub", ContaId.ToString()),
                new Claim("tipo_conta", "Aluno"),
                new Claim("perfil_id", PerfilId.ToString()),
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAluno"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "TestAluno")));
        }
    }
}
