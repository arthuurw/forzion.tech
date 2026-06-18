using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;
using forzion.tech.Application.UseCases.Pagamentos.ListarRecebimentosTreinador;
using forzion.tech.Application.UseCases.Treinadores.GerarCobrancaPlanoTreinador;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

public class PagamentosEndpointsTests : IClassFixture<PagamentosEndpointsTests.PagamentosWebFactory>
{
    private static readonly Guid ContaId = Guid.NewGuid();
    private static readonly Guid PerfilId = Guid.NewGuid();
    private readonly PagamentosWebFactory _factory;

    public PagamentosEndpointsTests(PagamentosWebFactory factory) => _factory = factory;

    private HttpClient ClienteTreinador()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", ContaId.ToString());
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
    public async Task Get_Recebimentos_SemToken_Retorna401()
    {
        var response = await _factory.CreateClient()
            .GetAsync("/treinador/pagamentos/recebimentos");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
                        return new ListarRecebimentosTreinadorResultado([], null);
                    });
                services.AddScoped(_ => recebimentosMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TreinadorTestAuthHandler>("Test", _ => { });
            });
        }
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
}
