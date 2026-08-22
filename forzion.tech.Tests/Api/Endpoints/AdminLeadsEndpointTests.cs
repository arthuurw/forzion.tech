using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

// e2e in-process (sem Docker) de GET/POST /admin/leads. ILeadRepository/ILogAprovacaoRepository
// mockados; os handlers reais rodam. O caso 403-para-não-admin já é coberto pela matriz
// genérica (AutorizacaoNegativaMatrixTests) — aqui o foco é o caminho feliz autenticado.
public class AdminLeadsEndpointTests : IClassFixture<AdminLeadsEndpointTests.AdminLeadsWebFactory>
{
    private readonly AdminLeadsWebFactory _factory;
    private static readonly Guid AdminContaId = Guid.NewGuid();

    public AdminLeadsEndpointTests(AdminLeadsWebFactory factory)
    {
        _factory = factory;
        _factory.LeadRepositoryMock.Reset();
        _factory.LogAprovacaoRepositoryMock.Reset();
        _factory.UnitOfWorkMock.Reset();
    }

    private HttpClient ClienteAdmin()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "admin");
        return client;
    }

    private static Lead NovoLead(Guid treinadorId) =>
        Lead.Criar(
            treinadorId, "Fulano",
            ContatoLead.Criar(TipoContatoLead.Email, "fulano@lead.com").Value,
            null,
            ConsentimentoLead.Criar("Contato comercial", DateTime.UtcNow, DateTime.UtcNow).Value,
            null, LeadSource.Agent, null, null, DateTime.UtcNow).Value;

    [Fact]
    public async Task Get_Leads_ComContatoEncontrado_Retorna200ComLista()
    {
        var lead = NovoLead(Guid.NewGuid());
        _factory.LeadRepositoryMock.Setup(r => r.BuscarPorContatoCrossTenantAsync("fulano@lead.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead]);

        var response = await ClienteAdmin().GetAsync("/admin/leads?contato=fulano@lead.com");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_Leads_SemResultado_Retorna200ComListaVazia()
    {
        _factory.LeadRepositoryMock.Setup(r => r.BuscarPorContatoCrossTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await ClienteAdmin().GetAsync("/admin/leads?contato=naoexiste@lead.com");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_Anonimizar_LeadExistente_Retorna204EGravaLog()
    {
        var lead = NovoLead(Guid.NewGuid());
        _factory.LeadRepositoryMock.Setup(r => r.ObterPorIdCrossTenantAsync(lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var response = await ClienteAdmin().PostAsync($"/admin/leads/{lead.Id}/anonimizar", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.LogAprovacaoRepositoryMock.Verify(r => r.AdicionarAsync(
            It.Is<LogAprovacao>(l => l.TipoAcao == TipoAcaoAprovacao.AnonimizacaoLead && l.EntidadeId == lead.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Post_Anonimizar_LeadInexistente_Retorna404()
    {
        _factory.LeadRepositoryMock.Setup(r => r.ObterPorIdCrossTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        var response = await ClienteAdmin().PostAsync($"/admin/leads/{Guid.NewGuid()}/anonimizar", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    public class AdminLeadsWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ILeadRepository> LeadRepositoryMock { get; } = new();
        public Mock<ILogAprovacaoRepository> LogAprovacaoRepositoryMock { get; } = new();
        public Mock<IUnitOfWork> UnitOfWorkMock { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILeadRepository>();
                services.RemoveAll<ILogAprovacaoRepository>();
                services.RemoveAll<IUnitOfWork>();

                services.AddScoped(_ => LeadRepositoryMock.Object);
                services.AddScoped(_ => LogAprovacaoRepositoryMock.Object);
                services.AddScoped(_ => UnitOfWorkMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, AdminLeadsTestAuthHandler>("Test", _ => { });
            });
        }
    }

    public class AdminLeadsTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public AdminLeadsTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(header))
                return Task.FromResult(AuthenticateResult.Fail("Sem token"));

            var claims = new[]
            {
                new Claim("sub", AdminContaId.ToString()),
                new Claim("tipo_conta", "SystemAdmin"),
                new Claim("perfil_id", AdminContaId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
        }
    }
}
