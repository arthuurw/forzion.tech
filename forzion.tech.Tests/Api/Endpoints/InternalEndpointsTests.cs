using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Application.Settings;
using forzion.tech.Application.UseCases.Conta.Lgpd;
using forzion.tech.Application.UseCases.Nfse.GerarNfseComissaoMensal;
using forzion.tech.Domain.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace forzion.tech.Tests.Api.Endpoints;

public class InternalEndpointsTests(InternalEndpointsTests.InternalWebFactory factory)
    : IClassFixture<InternalEndpointsTests.InternalWebFactory>
{
    private const string ChaveValida = "chave-interna-de-teste";

    public class InternalWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ListarContasElegivelPurgaLgpdHandler> ListarElegiveisMock { get; } =
            new(Mock.Of<IContaRepository>(), TimeProvider.System);

        public Mock<AnonimizarContaHandler> AnonimizarMock { get; } = new(
            Mock.Of<IContaRepository>(), Mock.Of<IAlunoRepository>(), Mock.Of<ITreinadorRepository>(),
            Mock.Of<IVinculoTreinadorAlunoRepository>(), Mock.Of<IExecucaoTreinoRepository>(),
            Mock.Of<IAssinanteRepository>(), Mock.Of<IEmailDeliveryLogRepository>(),
            Mock.Of<IWhatsAppDeliveryLogRepository>(), Mock.Of<IMensagemSuporteRepository>(),
            Mock.Of<ILogAprovacaoRepository>(),
            Mock.Of<IPasswordHasher>(), Mock.Of<IUnitOfWork>(), Mock.Of<IDbContextTransactionProvider>(),
            TimeProvider.System, Mock.Of<IUserContext>(), Mock.Of<ITokenRevogadoRepository>(),
            Mock.Of<IDatabaseErrorInspector>(), Mock.Of<IRefreshTokenFamilyRepository>());

        public Mock<GerarNfseComissaoMensalHandler> GerarNfseComissaoMock { get; } = new(
            Mock.Of<IPagamentoRepository>(), Mock.Of<INotaFiscalRepository>(), Mock.Of<IOutboxEnfileirador>(),
            Mock.Of<IUnitOfWork>(), Options.Create(new PaymentSettings()), TimeProvider.System,
            Mock.Of<ILogger<GerarNfseComissaoMensalHandler>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");
            builder.UseSetting("Internal:ApiKey", ChaveValida);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ListarContasElegivelPurgaLgpdHandler>();
                services.RemoveAll<AnonimizarContaHandler>();
                services.RemoveAll<GerarNfseComissaoMensalHandler>();
                services.AddSingleton(ListarElegiveisMock.Object);
                services.AddSingleton(AnonimizarMock.Object);
                services.AddSingleton(GerarNfseComissaoMock.Object);
            });
        }
    }

    [Fact]
    public async Task GetContasElegiveis_ComChave_Retorna200()
    {
        factory.ListarElegiveisMock
            .Setup(h => h.HandleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Guid.NewGuid() });

        var req = new HttpRequestMessage(HttpMethod.Get, "/internal/lgpd/contas-elegiveis");
        req.Headers.Add("X-Internal-Key", ChaveValida);
        var response = await factory.CreateClient().SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetContasElegiveis_SemChave_Retorna401()
    {
        var response = await factory.CreateClient().GetAsync("/internal/lgpd/contas-elegiveis");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteConta_ComChave_Retorna204()
    {
        factory.AnonimizarMock
            .Setup(h => h.HandleAsync(It.IsAny<AnonimizarContaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var req = new HttpRequestMessage(HttpMethod.Delete, $"/internal/lgpd/contas/{Guid.NewGuid()}");
        req.Headers.Add("X-Internal-Key", ChaveValida);
        var response = await factory.CreateClient().SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteConta_SemChave_Retorna401()
    {
        var response = await factory.CreateClient().DeleteAsync($"/internal/lgpd/contas/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GerarNfseComissao_ComChave_Retorna200()
    {
        factory.GerarNfseComissaoMock
            .Setup(h => h.HandleAsync(It.IsAny<GerarNfseComissaoMensalCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new GerarNfseComissaoMensalResultado(2, 1)));

        var req = new HttpRequestMessage(HttpMethod.Post, "/internal/gerar-nfse-comissao");
        req.Headers.Add("X-Internal-Key", ChaveValida);
        var response = await factory.CreateClient().SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GerarNfseComissao_SemChave_Retorna401()
    {
        var response = await factory.CreateClient().PostAsync("/internal/gerar-nfse-comissao", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
