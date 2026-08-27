using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using forzion.tech.Api.Endpoints.Leads;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Leads.AtualizarStatusLead;
using forzion.tech.Application.UseCases.Leads.CriarLeadManual;
using forzion.tech.Application.UseCases.Leads.ListarLeads;
using forzion.tech.Application.UseCases.Leads.ObterLead;
using forzion.tech.Application.UseCases.Leads.ObterMetricas;
using forzion.tech.Application.UseCases.Leads.RegistrarInteracao;
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

// e2e in-process (sem Docker) das 7 rotas de /treinador/leads. ILeadRepository/IUnitOfWork
// mockados via DI; os handlers reais rodam para exercitar de fato o colapso 404 de IDOR
// (lead de outro treinador -> 404, nunca 403 — mesmo padrão de DefinirPreservacaoVinculoEndpointTests).
public class LeadEndpointsTests : IClassFixture<LeadEndpointsTests.LeadWebFactory>
{
    private readonly LeadWebFactory _factory;

    private static readonly Guid TreinadorAutenticadoId = Guid.NewGuid();
    private static readonly DateTime Agora = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    public LeadEndpointsTests(LeadWebFactory factory)
    {
        _factory = factory;
        _factory.LeadRepositoryMock.Reset();
        _factory.UnitOfWorkMock.Reset();
        _factory.LeadConviteRepositoryMock.Reset();
        _factory.TreinadorRepositoryMock.Reset();
        _factory.LeadConviteSenderMock.Reset();
    }

    private HttpClient ClienteTreinador()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "treinador");
        return client;
    }

    private static Lead NovoLead(Guid treinadorId) =>
        Lead.Criar(
            treinadorId,
            "Fulano",
            ContatoLead.Criar(TipoContatoLead.Email, "fulano@lead.com").Value,
            "quero treinar",
            ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value,
            null,
            LeadSource.Agent,
            null,
            null,
            Agora).Value;

    [Fact]
    public async Task Get_Lista_Retorna200ComItens()
    {
        _factory.LeadRepositoryMock.Setup(r => r.ListarAsync(
                TreinadorAutenticadoId, 1, 20, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<LeadListItem>(), 0));

        var response = await ClienteTreinador().GetAsync("/treinador/leads");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListarLeadsResponse>();
        body!.Total.Should().Be(0);
    }

    [Fact]
    public async Task Get_Detalhe_LeadProprio_Retorna200()
    {
        var lead = NovoLead(TreinadorAutenticadoId);
        _factory.LeadRepositoryMock.Setup(r => r.ObterComHistoricoAsync(TreinadorAutenticadoId, lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var response = await ClienteTreinador().GetAsync($"/treinador/leads/{lead.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_Detalhe_LeadDeOutroTreinador_Retorna404()
    {
        var leadDeOutro = NovoLead(Guid.NewGuid());

        var response = await ClienteTreinador().GetAsync($"/treinador/leads/{leadDeOutro.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_CadastroManual_Retorna201()
    {
        var response = await ClienteTreinador().PostAsJsonAsync("/treinador/leads", new CriarLeadManualRequest(
            "Fulano", TipoContatoLead.Email, "fulano@lead.com", "quero treinar", "Contato comercial"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _factory.UnitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Patch_Status_LeadProprio_Retorna200()
    {
        var lead = NovoLead(TreinadorAutenticadoId);
        _factory.LeadRepositoryMock.Setup(r => r.ObterComHistoricoAsync(TreinadorAutenticadoId, lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var response = await ClienteTreinador().PatchAsJsonAsync(
            $"/treinador/leads/{lead.Id}/status", new AtualizarStatusLeadRequest(LeadStatus.EmContato, null, "liguei"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.UnitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Patch_Status_LeadDeOutroTreinador_Retorna404()
    {
        var leadDeOutro = NovoLead(Guid.NewGuid());

        var response = await ClienteTreinador().PatchAsJsonAsync(
            $"/treinador/leads/{leadDeOutro.Id}/status", new AtualizarStatusLeadRequest(LeadStatus.EmContato, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _factory.UnitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Post_Interacao_LeadProprio_Retorna200()
    {
        var lead = NovoLead(TreinadorAutenticadoId);
        _factory.LeadRepositoryMock.Setup(r => r.ObterComHistoricoAsync(TreinadorAutenticadoId, lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var response = await ClienteTreinador().PostAsJsonAsync(
            $"/treinador/leads/{lead.Id}/interacoes", new RegistrarInteracaoLeadRequest("liguei, sem resposta"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_Interacao_LeadDeOutroTreinador_Retorna404()
    {
        var leadDeOutro = NovoLead(Guid.NewGuid());

        var response = await ClienteTreinador().PostAsJsonAsync(
            $"/treinador/leads/{leadDeOutro.Id}/interacoes", new RegistrarInteracaoLeadRequest("nota"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_Convite_LeadProprioComEmail_Retorna200()
    {
        var lead = NovoLead(TreinadorAutenticadoId);
        _factory.LeadRepositoryMock.Setup(r => r.ObterComHistoricoAsync(TreinadorAutenticadoId, lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);
        _factory.TreinadorRepositoryMock.Setup(r => r.ObterPorIdAsync(TreinadorAutenticadoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Treinador.Criar(Guid.NewGuid(), "Coach", Agora).Value);
        _factory.LeadConviteSenderMock.Setup(s => s.EnviarAsync(
                It.IsAny<TipoContatoLead>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await ClienteTreinador().PostAsync($"/treinador/leads/{lead.Id}/convite", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_Convite_ContatoTelefone_Retorna422()
    {
        var lead = Lead.Criar(
            TreinadorAutenticadoId, "Fulano",
            ContatoLead.Criar(TipoContatoLead.Telefone, "+5511999998888").Value,
            null,
            ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value,
            null, LeadSource.Agent, null, null, Agora).Value;
        _factory.LeadRepositoryMock.Setup(r => r.ObterComHistoricoAsync(TreinadorAutenticadoId, lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var response = await ClienteTreinador().PostAsync($"/treinador/leads/{lead.Id}/convite", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_Convite_LeadDeOutroTreinador_Retorna404()
    {
        var leadDeOutro = NovoLead(Guid.NewGuid());

        var response = await ClienteTreinador().PostAsync($"/treinador/leads/{leadDeOutro.Id}/convite", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Metricas_Retorna200()
    {
        _factory.LeadRepositoryMock.Setup(r => r.AgregarMetricasAsync(
                TreinadorAutenticadoId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeadMetricas(0, 0, 0, 0, new Dictionary<MotivoDescarteLead, int>()));

        var response = await ClienteTreinador().GetAsync("/treinador/leads/metricas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ObterMetricasLeadsResponse>();
        body!.Total.Should().Be(0);
    }

    [Fact]
    public async Task Get_Metricas_SemParametros_FimCobreODiaCorrenteInteiro()
    {
        DateTime? fimCapturado = null;
        _factory.LeadRepositoryMock.Setup(r => r.AgregarMetricasAsync(
                TreinadorAutenticadoId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTime, DateTime, CancellationToken>((_, _, fim, _) => fimCapturado = fim)
            .ReturnsAsync(new LeadMetricas(0, 0, 0, 0, new Dictionary<MotivoDescarteLead, int>()));

        var antesDaChamada = DateTime.UtcNow;
        var response = await ClienteTreinador().GetAsync("/treinador/leads/metricas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fimCapturado.Should().NotBeNull();
        fimCapturado!.Value.Should().BeOnOrAfter(antesDaChamada,
            "um lead criado agora mesmo (CreatedAt <= fim) precisa continuar dentro da janela default");
        fimCapturado.Value.Date.Should().Be(antesDaChamada.Date);
    }

    [Fact]
    public async Task Get_Metricas_ComFimExplicito_CobreODiaInteiro()
    {
        DateTime? fimCapturado = null;
        _factory.LeadRepositoryMock.Setup(r => r.AgregarMetricasAsync(
                TreinadorAutenticadoId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTime, DateTime, CancellationToken>((_, _, fim, _) => fimCapturado = fim)
            .ReturnsAsync(new LeadMetricas(0, 0, 0, 0, new Dictionary<MotivoDescarteLead, int>()));

        var response = await ClienteTreinador().GetAsync("/treinador/leads/metricas?fim=2026-08-20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fimCapturado.Should().NotBeNull();
        var leadCriadoNoFimDoDia = new DateTime(2026, 8, 20, 23, 59, 0, DateTimeKind.Utc);
        fimCapturado!.Value.Should().BeOnOrAfter(leadCriadoNoFimDoDia,
            "um 'fim' explícito também precisa cobrir o dia inteiro, não só a meia-noite");
    }

    [Fact]
    public async Task Get_Metricas_InicioMaiorQueFim_Retorna400()
    {
        var response = await ClienteTreinador().GetAsync("/treinador/leads/metricas?inicio=2026-08-20&fim=2026-08-10");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Lista_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().GetAsync("/treinador/leads");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public class LeadWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ILeadRepository> LeadRepositoryMock { get; } = new();
        public Mock<IUnitOfWork> UnitOfWorkMock { get; } = new();
        public Mock<ILeadConviteRepository> LeadConviteRepositoryMock { get; } = new();
        public Mock<ITreinadorRepository> TreinadorRepositoryMock { get; } = new();
        public Mock<ILeadConviteSender> LeadConviteSenderMock { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILeadRepository>();
                services.RemoveAll<IUnitOfWork>();
                services.RemoveAll<IUserContext>();
                services.RemoveAll<ILeadConviteRepository>();
                services.RemoveAll<ITreinadorRepository>();
                services.RemoveAll<ILeadConviteSender>();

                services.AddScoped(_ => LeadRepositoryMock.Object);
                services.AddScoped(_ => UnitOfWorkMock.Object);
                services.AddScoped(_ => LeadConviteRepositoryMock.Object);
                services.AddScoped(_ => TreinadorRepositoryMock.Object);
                services.AddScoped(_ => LeadConviteSenderMock.Object);

                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.PerfilId).Returns(TreinadorAutenticadoId);
                userContextMock.Setup(u => u.TipoConta).Returns(TipoConta.Treinador);
                services.AddScoped(_ => userContextMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, LeadTestAuthHandler>("Test", _ => { });
            });
        }
    }

    public class LeadTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public LeadTestAuthHandler(
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
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("tipo_conta", "Treinador"),
                new Claim("perfil_id", TreinadorAutenticadoId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
        }
    }
}
