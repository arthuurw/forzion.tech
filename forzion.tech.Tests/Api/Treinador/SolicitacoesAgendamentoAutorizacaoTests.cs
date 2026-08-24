using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Tests.Builders;
using forzion.tech.Tests.E2E;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// SPEC_DEVIATION: namespace "Treinadores" (plural), não "Treinador" — um segmento de namespace
// igual ao nome da classe forzion.tech.Domain.Entities.Treinador quebra a resolução de tipo
// unqualified em TODO arquivo irmão sob forzion.tech.Tests.Api.* que usa "Treinador" via
// `using forzion.tech.Domain.Entities;` (CS0118). Mesma convenção já usada em
// Application/UseCases/Treinadores e nos testes espelho.
namespace forzion.tech.Tests.Api.Treinadores;

// Cross-tenant da esteira de solicitações (T22): treinadorId sempre no predicado do
// repositório, nunca no corpo/rota — acesso de A a recurso de B colapsa em 404, nunca 403
// (AGF4-24), mesmo padrão de isolamento já provado pela fatia 3 (AgendaRotasE2ETests).
[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class SolicitacoesAgendamentoAutorizacaoTests(RealPipelineFixture fixture)
{
    private const string SenhaPadrao = "SenhaForte123";

    [Fact]
    public async Task TreinadorA_ListaSuasSolicitacoes_NaoVeSolicitacaoDeTreinadorB()
    {
        var (_, clienteA) = await TreinadorAprovadoComClienteAsync();
        var treinadorB = await SeedTreinadorAsync();
        var (pacoteB, leadB) = await SeedPacoteELeadAsync(treinadorB);
        var solicitacaoB = await SeedSolicitacaoPendenteAsync(treinadorB, pacoteB, leadB, DateTime.UtcNow.AddDays(1));

        var listar = await clienteA.GetAsync("/treinador/agenda/solicitacoes");

        listar.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await listar.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("items").EnumerateArray()
            .Should().NotContain(i => i.GetProperty("id").GetGuid() == solicitacaoB);
    }

    [Fact]
    public async Task TreinadorA_ConfirmaSolicitacaoDeTreinadorB_Retorna404NaoForbidden()
    {
        var (_, clienteA) = await TreinadorAprovadoComClienteAsync();
        var treinadorB = await SeedTreinadorAsync();
        var (pacoteB, leadB) = await SeedPacoteELeadAsync(treinadorB);
        var solicitacaoB = await SeedSolicitacaoPendenteAsync(treinadorB, pacoteB, leadB, DateTime.UtcNow.AddDays(1));

        var resposta = await clienteA.PostAsync($"/treinador/agenda/solicitacoes/{solicitacaoB}/confirmar", null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resposta.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        await AssertaCorpoGenericoSemVazamentoAsync(resposta);
    }

    [Fact]
    public async Task TreinadorA_RecusaSolicitacaoDeTreinadorB_Retorna404NaoForbidden()
    {
        var (_, clienteA) = await TreinadorAprovadoComClienteAsync();
        var treinadorB = await SeedTreinadorAsync();
        var (pacoteB, leadB) = await SeedPacoteELeadAsync(treinadorB);
        var solicitacaoB = await SeedSolicitacaoPendenteAsync(treinadorB, pacoteB, leadB, DateTime.UtcNow.AddDays(1));

        var resposta = await clienteA.PostAsJsonAsync($"/treinador/agenda/solicitacoes/{solicitacaoB}/recusar", new { });

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resposta.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        await AssertaCorpoGenericoSemVazamentoAsync(resposta);
    }

    [Fact]
    public async Task TreinadorA_CancelaSolicitacaoDeTreinadorB_Retorna404NaoForbidden()
    {
        var (_, clienteA) = await TreinadorAprovadoComClienteAsync();
        var treinadorB = await SeedTreinadorAsync();
        var (pacoteB, leadB) = await SeedPacoteELeadAsync(treinadorB);
        var solicitacaoB = await SeedSolicitacaoPendenteAsync(treinadorB, pacoteB, leadB, DateTime.UtcNow.AddDays(1));
        await ConfirmarDiretoNoBancoAsync(solicitacaoB);

        var resposta = await clienteA.PostAsJsonAsync($"/treinador/agenda/solicitacoes/{solicitacaoB}/cancelar", new { });

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resposta.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        await AssertaCorpoGenericoSemVazamentoAsync(resposta);
    }

    [Fact]
    public async Task TreinadorA_ConfirmaPropriaSolicitacaoComSlotJaIniciado_EhRejeitada()
    {
        var (treinadorA, clienteA) = await TreinadorAprovadoComClienteAsync();
        var (pacoteA, leadA) = await SeedPacoteELeadAsync(treinadorA);
        var solicitacaoA = await SeedSolicitacaoPendenteAsync(treinadorA, pacoteA, leadA, DateTime.UtcNow.AddMinutes(-10));

        var resposta = await clienteA.PostAsync($"/treinador/agenda/solicitacoes/{solicitacaoA}/confirmar", null);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static async Task AssertaCorpoGenericoSemVazamentoAsync(HttpResponseMessage resposta)
    {
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        corpo.GetProperty("code").GetString().Should().Be("solicitacao_agendamento.nao_encontrada");
        corpo.TryGetProperty("treinadorId", out _).Should().BeFalse();
    }

    // --- Seed direto no banco (sem passar pela cadeia do agente — fora de escopo aqui, T28 prova a cadeia) ---

    private async Task<Guid> SeedTreinadorAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agora = DateTime.UtcNow;

        var email = Email.Criar($"tb{Guid.NewGuid():N}@e2e.test").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, agora).Value;
        var treinador = forzion.tech.Domain.Entities.Treinador.Criar(conta.Id, "Treinador B", agora).Value;
        db.Contas.Add(conta);
        db.Treinadores.Add(treinador);
        await db.SaveChangesAsync();
        return treinador.Id;
    }

    private async Task<(Guid PacoteId, Guid LeadId)> SeedPacoteELeadAsync(Guid treinadorId)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agora = DateTime.UtcNow;

        var pacote = new PacoteBuilder().ComTreinadorId(treinadorId).Em(agora).Build();
        db.Pacotes.Add(pacote);

        var contato = ContatoLead.Criar(TipoContatoLead.Email, $"lead{Guid.NewGuid():N}@e2e.test").Value;
        var consentimento = ConsentimentoLead.Criar("Contato comercial", agora, agora).Value;
        var lead = Lead.Criar(treinadorId, "Lead Autorizacao", contato, null, consentimento, null, LeadSource.Agent, null, null, agora).Value;
        db.Leads.Add(lead);

        await db.SaveChangesAsync();
        return (pacote.Id, lead.Id);
    }

    private async Task<Guid> SeedSolicitacaoPendenteAsync(Guid treinadorId, Guid pacoteId, Guid leadId, DateTime inicioUtc)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agora = DateTime.UtcNow;

        var solicitacao = SolicitacaoAgendamento.Criar(
            treinadorId, pacoteId, leadId, $"slot-{Guid.NewGuid():N}", inicioUtc, inicioUtc.AddMinutes(60),
            $"idem-{Guid.NewGuid():N}", "hash", agora).Value;
        db.SolicitacoesAgendamento.Add(solicitacao);

        await db.SaveChangesAsync();
        return solicitacao.Id;
    }

    private async Task ConfirmarDiretoNoBancoAsync(Guid solicitacaoId)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var solicitacao = await db.SolicitacoesAgendamento.FirstAsync(s => s.Id == solicitacaoId);
        solicitacao.Confirmar(Guid.NewGuid(), DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    // --- Helpers de auth/gestão (mesmo padrão duplicado dos outros E2E — sem base compartilhada no repo) ---

    private async Task<(Guid TreinadorId, HttpClient Cliente)> TreinadorAprovadoComClienteAsync()
    {
        var (treinadorId, email) = await RegistrarTreinadorAsync();
        var admin = ClienteComToken(await LoginTokenAsync(RealPipelineFixture.AdminEmail, RealPipelineFixture.AdminPassword));

        using var req = new HttpRequestMessage(HttpMethod.Post, $"/admin/treinadores/{treinadorId}/aprovar")
        {
            Content = JsonContent.Create(new { }),
        };
        req.Headers.Add(forzion.tech.Api.Filters.RequerStepUpFilter.Header, await fixture.GerarStepUpTokenAsync(RealPipelineFixture.AdminEmail));
        (await admin.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.OK);

        return (treinadorId, ClienteComToken(await LoginTokenAsync(email)));
    }

    private async Task<(Guid TreinadorId, string Email)> RegistrarTreinadorAsync()
    {
        var email = $"ta{Guid.NewGuid():N}@e2e.test";
        var planos = await fixture.CreateClient().GetFromJsonAsync<JsonElement>("/auth/planos");
        var planoFreeId = planos.EnumerateArray().First(p => p.GetProperty("nome").GetString() == "Free").GetProperty("planoId").GetGuid();

        var response = await fixture.CreateClient().PostAsJsonAsync(
            "/auth/register/treinador",
            new { email, senha = SenhaPadrao, nome = "Treinador E2E Autorizacao", planoPlataformaId = planoFreeId, modoPagamentoAluno = "Plataforma" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var treinadorId = body.GetProperty("treinadorId").GetGuid();

        using var scope = fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<forzion.tech.Application.Interfaces.Repositories.IContaRepository>();
        var conta = await repo.ObterPorEmailAsync(email.Trim().ToLowerInvariant());
        conta!.MarcarEmailVerificado(DateTime.UtcNow);
        await scope.ServiceProvider.GetRequiredService<forzion.tech.Application.Interfaces.IUnitOfWork>().CommitAsync();

        return (treinadorId, email);
    }

    private async Task<string> LoginTokenAsync(string email) => await LoginTokenAsync(email, SenhaPadrao);

    private async Task<string> LoginTokenAsync(string email, string senha)
    {
        var response = await fixture.CreateClient().PostAsJsonAsync("/auth/login", new { email, senha });
        response.StatusCode.Should().Be(HttpStatusCode.OK, "login deve funcionar para {0}", email);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private HttpClient ClienteComToken(string token)
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
