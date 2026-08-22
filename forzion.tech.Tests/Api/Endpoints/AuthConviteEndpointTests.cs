using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

// e2e in-process (sem Docker) do GET /auth/convite/{token}, anônimo. Repositórios mockados;
// ResolverConviteLeadHandler roda real para provar o colapso 404 dos três casos (inexistente,
// expirado, consumido) — nenhum deles pode ser distinguível de fora (AGF2-42).
public class AuthConviteEndpointTests : IClassFixture<AuthConviteEndpointTests.ConviteWebFactory>
{
    private readonly ConviteWebFactory _factory;

    // Endpoint roda com o TimeProvider real do processo (não fake) — a base precisa acompanhar
    // o relógio de parede, não uma data fixa que expira sozinha conforme os anos passam.
    private static readonly DateTime Agora = DateTime.UtcNow.AddDays(-1);
    private static readonly Guid TreinadorId = Guid.NewGuid();

    public AuthConviteEndpointTests(ConviteWebFactory factory)
    {
        _factory = factory;
        _factory.LeadConviteRepositoryMock.Reset();
        _factory.LeadRepositoryMock.Reset();
        _factory.TreinadorRepositoryMock.Reset();
    }

    private static Lead NovoLead() =>
        Lead.Criar(
            TreinadorId, "Fulano",
            ContatoLead.Criar(TipoContatoLead.Email, "fulano@lead.com").Value,
            "quero treinar",
            ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value,
            null, LeadSource.Agent, null, null, Agora).Value;

    private static LeadConvite ConviteValido(Guid leadId) =>
        LeadConvite.Criar(leadId, TreinadorId, "hash-do-token", Agora.AddDays(14), Agora).Value;

    [Fact]
    public async Task Get_TokenValido_Retorna200ComDadosPrePreenchidos()
    {
        var lead = NovoLead();
        var convite = ConviteValido(lead.Id);
        _factory.LeadConviteRepositoryMock.Setup(r => r.ObterPorTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(convite);
        _factory.LeadRepositoryMock.Setup(r => r.ObterComHistoricoAsync(TreinadorId, lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);
        _factory.TreinadorRepositoryMock.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Treinador.Criar(Guid.NewGuid(), "Coach Carlos", Agora).Value);

        var response = await _factory.CreateClient().GetAsync("/auth/convite/token-cru-valido");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("nome").GetString().Should().Be("Fulano");
        body.GetProperty("treinadorNome").GetString().Should().Be("Coach Carlos");
    }

    [Fact]
    public async Task Get_TokenInexistente_Retorna404()
    {
        _factory.LeadConviteRepositoryMock.Setup(r => r.ObterPorTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadConvite?)null);

        var response = await _factory.CreateClient().GetAsync("/auth/convite/token-que-nao-existe");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_TokenExpirado_Retorna404IgualAoInexistente()
    {
        // Handler compara ExpiraEm contra o TimeProvider real do processo (wall-clock) — cria o
        // convite inteiramente no passado para já nascer expirado sem depender de Task.Delay.
        var passadoDistante = DateTime.UtcNow.AddDays(-20);
        var lead = Lead.Criar(
            TreinadorId, "Fulano",
            ContatoLead.Criar(TipoContatoLead.Email, "fulano@lead.com").Value,
            "quero treinar",
            ConsentimentoLead.Criar("Contato comercial", passadoDistante, passadoDistante).Value,
            null, LeadSource.Agent, null, null, passadoDistante).Value;
        var expirado = LeadConvite.Criar(lead.Id, TreinadorId, "hash", passadoDistante.AddDays(14), passadoDistante).Value;
        _factory.LeadConviteRepositoryMock.Setup(r => r.ObterPorTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expirado);

        var response = await _factory.CreateClient().GetAsync("/auth/convite/token-expirado");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_TokenJaConsumido_Retorna404IgualAoInexistente()
    {
        var lead = NovoLead();
        var consumido = LeadConvite.Criar(lead.Id, TreinadorId, "hash", Agora.AddDays(14), Agora).Value;
        consumido.Consumir(Agora.AddHours(1));
        _factory.LeadConviteRepositoryMock.Setup(r => r.ObterPorTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(consumido);

        var response = await _factory.CreateClient().GetAsync("/auth/convite/token-consumido");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_TokenInvalidado_Retorna404IgualAoInexistente()
    {
        var lead = NovoLead();
        var invalidado = LeadConvite.Criar(lead.Id, TreinadorId, "hash", Agora.AddDays(14), Agora).Value;
        invalidado.Invalidar(Agora.AddHours(1));
        _factory.LeadConviteRepositoryMock.Setup(r => r.ObterPorTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invalidado);

        var response = await _factory.CreateClient().GetAsync("/auth/convite/token-invalidado");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    public class ConviteWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ILeadConviteRepository> LeadConviteRepositoryMock { get; } = new();
        public Mock<ILeadRepository> LeadRepositoryMock { get; } = new();
        public Mock<ITreinadorRepository> TreinadorRepositoryMock { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILeadConviteRepository>();
                services.RemoveAll<ILeadRepository>();
                services.RemoveAll<ITreinadorRepository>();

                services.AddScoped(_ => LeadConviteRepositoryMock.Object);
                services.AddScoped(_ => LeadRepositoryMock.Object);
                services.AddScoped(_ => TreinadorRepositoryMock.Object);
            });
        }
    }
}
