using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Application.Settings;
using forzion.tech.Application.UseCases.Pagamentos;
using forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;
using forzion.tech.Application.UseCases.Treinadores.GerarCobrancaPlanoTreinador;
using forzion.tech.Application.UseCases.Treinadores.IniciarPagamentoPlano;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace forzion.tech.Tests.Api.Endpoints;

public class ProcessarRenovacoesEndpointsTests(ProcessarRenovacoesEndpointsTests.RenovacoesWebFactory factory)
    : IClassFixture<ProcessarRenovacoesEndpointsTests.RenovacoesWebFactory>
{
    private const string ChaveValida = "chave-interna-de-teste";

    public class RenovacoesWebFactory : WebApplicationFactory<Program>
    {
        private static readonly CriarPagamentoComIntentService CriarServiceStub = new(
            Mock.Of<IUnitOfWork>(), Mock.Of<IDbContextTransactionProvider>(),
            Mock.Of<IDatabaseErrorInspector>(), TimeProvider.System,
            Mock.Of<ILogger<CriarPagamentoComIntentService>>());

        public Mock<IAssinaturaAlunoRepository> AssinaturaAlunoRepoMock { get; } = new();
        public Mock<IAssinaturaTreinadorRepository> AssinaturaTreinadorRepoMock { get; } = new();

        public Mock<GerarCobrancaMensalHandler> GerarCobrancaMensalMock { get; } = new(
            Mock.Of<IAssinaturaAlunoRepository>(), Mock.Of<IPagamentoRepository>(),
            Mock.Of<IContaRecebimentoRepository>(), Mock.Of<IStripeService>(),
            CriarServiceStub, Options.Create(new PaymentSettings()), TimeProvider.System,
            Mock.Of<ILogger<GerarCobrancaMensalHandler>>());

        public Mock<GerarCobrancaPlanoTreinadorHandler> GerarCobrancaTreinadorMock { get; } = new(
            Mock.Of<IAssinaturaTreinadorRepository>(), Mock.Of<IPagamentoTreinadorRepository>(),
            Mock.Of<IPlanoPlataformaRepository>(), Mock.Of<IStripeService>(), Mock.Of<IUnitOfWork>(),
            CriarServiceStub, TimeProvider.System,
            Mock.Of<ILogger<GerarCobrancaPlanoTreinadorHandler>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");
            builder.UseSetting("Internal:ApiKey", ChaveValida);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAssinaturaAlunoRepository>();
                services.RemoveAll<IAssinaturaTreinadorRepository>();
                services.RemoveAll<GerarCobrancaMensalHandler>();
                services.RemoveAll<GerarCobrancaPlanoTreinadorHandler>();
                services.AddSingleton(AssinaturaAlunoRepoMock.Object);
                services.AddSingleton(AssinaturaTreinadorRepoMock.Object);
                services.AddScoped(_ => GerarCobrancaMensalMock.Object);
                services.AddScoped(_ => GerarCobrancaTreinadorMock.Object);
            });
        }
    }

    [Fact]
    public async Task ProcessarRenovacoesAluno_ProcessaTodasEIsolaFalhas()
    {
        var assinaturas = new[]
        {
            AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, DateTime.UtcNow).Value,
            AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, DateTime.UtcNow).Value,
            AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, DateTime.UtcNow).Value,
        };
        factory.AssinaturaAlunoRepoMock
            .Setup(r => r.ListarParaRenovarAsync(It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinaturas);
        factory.GerarCobrancaMensalMock
            .Setup(h => h.HandleAsync(It.IsAny<GerarCobrancaMensalCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PagamentoResponse>(AssinaturaAlunoErrors.NaoEncontrada));

        var req = new HttpRequestMessage(HttpMethod.Post, "/internal/processar-renovacoes");
        req.Headers.Add("X-Internal-Key", ChaveValida);
        var response = await factory.CreateClient().SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("processadas").GetInt32().Should().Be(3);
        body.GetProperty("falhas").GetInt32().Should().Be(3);
        factory.GerarCobrancaMensalMock.Verify(
            h => h.HandleAsync(It.IsAny<GerarCobrancaMensalCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task ProcessarRenovacoesTreinador_ProcessaTodasEIsolaFalhas()
    {
        var assinaturas = new[]
        {
            AssinaturaTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), 100m, DateTime.UtcNow).Value,
            AssinaturaTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), 100m, DateTime.UtcNow).Value,
            AssinaturaTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), 100m, DateTime.UtcNow).Value,
        };
        factory.AssinaturaTreinadorRepoMock
            .Setup(r => r.ListarParaRenovarAsync(It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinaturas);
        factory.GerarCobrancaTreinadorMock
            .Setup(h => h.HandleAsync(It.IsAny<GerarCobrancaPlanoTreinadorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<IniciarPagamentoPlanoResponse>(AssinaturaTreinadorErrors.NaoEncontrada));

        var req = new HttpRequestMessage(HttpMethod.Post, "/internal/processar-renovacoes-treinador");
        req.Headers.Add("X-Internal-Key", ChaveValida);
        var response = await factory.CreateClient().SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("processadas").GetInt32().Should().Be(3);
        body.GetProperty("falhas").GetInt32().Should().Be(3);
        factory.GerarCobrancaTreinadorMock.Verify(
            h => h.HandleAsync(It.IsAny<GerarCobrancaPlanoTreinadorCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }
}
