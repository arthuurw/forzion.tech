using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Api.Endpoints.Agents;
using forzion.tech.Api.Endpoints.Agents.Hmac;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.E2E;

// Prova a cadeia inteira da fatia 4, ponta a ponta, sem mock em nenhuma ponta (AD-018/R1):
// POST booking-requests HMAC-assinado -> treinador confirma por HTTP real -> GET availability
// HMAC-assinado reflete a vaga a menos -> cancelar devolve a vaga. Moldes: PerfilPublicoWritePathE2ETests
// (auth/gestão), AgendaWritePathE2ETests (agenda por HTTP), LeadAgenteE2ETests (POST HMAC assinado).
[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class BookingRequestE2ETests(RealPipelineFixture fixture)
{
    private const string SenhaPadrao = "SenhaForte123";
    private static readonly TimeZoneInfo FusoSaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private readonly Dictionary<Guid, string> _emailPorTreinador = new();

    [Fact]
    public async Task CadeiaCompleta_PostAssinadoConfirmacaoHttpECapacidadeRefletidaNoAvailability()
    {
        var treinadorId = await TreinadorAprovadoAsync();
        var treinador = ClienteComToken(await LoginTokenAsync(treinadorId));
        var segundaLocal = ProximaSegundaLocal();

        // 1) Treinador real, publicado, com horário de funcionamento e pacote público/ativo —
        // capacidadeMaxima nasce 1 por padrão do domínio (não há campo de capacidade no wire, D-H/R4).
        var salvarPerfil = await treinador.PutAsJsonAsync("/treinador/perfil-publico", new
        {
            nomeFantasia = "Studio Cadeia E2E",
            endereco = (object?)null,
            politicas = (object?)null,
            horarios = new[] { new { diaSemana = 1, abreAs = "08:00", fechaAs = "11:00" } },
            isPublicado = true,
            fusoHorario = "America/Sao_Paulo",
        });
        salvarPerfil.StatusCode.Should().Be(HttpStatusCode.OK);

        var criarPacote = await treinador.PostAsJsonAsync("/treinador/pacotes", new
        {
            nome = "Aula experimental",
            preco = 120m,
            categoria = "Treino",
            duracaoMinutos = 60,
            trialDisponivel = true,
            isPublico = true,
        });
        criarPacote.StatusCode.Should().Be(HttpStatusCode.Created);
        var pacoteId = (await criarPacote.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("pacoteId").GetGuid();

        // 2) GET availability HMAC-assinado devolve slots; um slotId é copiado da resposta.
        var fromUtc = DateTime.SpecifyKind(segundaLocal.AddDays(-1), DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(segundaLocal.AddDays(2), DateTimeKind.Utc);
        var slotsIniciais = await ConsultarAvailabilityAsync(treinadorId, pacoteId, fromUtc, toUtc);
        slotsIniciais.Should().HaveCount(3);
        var slotEscolhido = slotsIniciais[0];
        var slotId = slotEscolhido.GetProperty("slotId").GetString()!;
        var inicioSlotUtc = DateTimeOffset.Parse(slotEscolhido.GetProperty("startsAt").GetString()!).UtcDateTime;

        // 3) POST booking-requests HMAC-assinado devolve 201 e cria lead + solicitação.
        var idempotencyKey = $"chave-{Guid.NewGuid():N}";
        var corpoBooking = $$"""
            {"serviceId":"{{pacoteId}}","slotId":"{{slotId}}","name":"Consumidor E2E",
            "contact":{"type":"email","value":"consumidor-e2e@teste.com"},
            "consent":{"granted":true,"purpose":"Contato comercial"},
            "idempotencyKey":"{{idempotencyKey}}"}
            """;
        using var respostaBooking = await EnviarAssinadaAsync(
            fixture.CreateClient(), CaminhoBookingRequests(treinadorId), corpoBooking);

        respostaBooking.StatusCode.Should().Be(HttpStatusCode.Created);
        using var corpoResposta = JsonDocument.Parse(await respostaBooking.Content.ReadAsStringAsync());
        corpoResposta.RootElement.GetProperty("status").GetString().Should().Be("pending-agent");
        var bookingRequestId = Guid.Parse(corpoResposta.RootElement.GetProperty("bookingRequestId").GetString()!);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var solicitacao = await db.SolicitacoesAgendamento.SingleAsync(s => s.Id == bookingRequestId);
            solicitacao.TreinadorId.Should().Be(treinadorId);
            solicitacao.PacoteId.Should().Be(pacoteId);
            solicitacao.Status.Should().Be(SolicitacaoAgendamentoStatus.PendenteAgente);
            var lead = await db.Leads.SingleAsync(l => l.Id == solicitacao.LeadId);
            lead.TreinadorId.Should().Be(treinadorId);
            lead.Source.Should().Be(LeadSource.Agent);
        }

        // 4) Treinador autenticado por JWT confirma por HTTP real.
        var confirmar = await treinador.PostAsJsonAsync($"/treinador/agenda/solicitacoes/{bookingRequestId}/confirmar", new { });
        confirmar.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(" || ", fixture.ErrosCapturados));

        // 5) Novo GET availability (mesma chamada HMAC-assinada) não traz mais aquele slot —
        // capacidadeMaxima 1 esgotada pela confirmada, sem mudança de schema (D-C/D-F).
        var slotsAposConfirmar = await ConsultarAvailabilityAsync(treinadorId, pacoteId, fromUtc, toUtc);
        slotsAposConfirmar.Select(s => DateTimeOffset.Parse(s.GetProperty("startsAt").GetString()!).UtcDateTime)
            .Should().NotContain(inicioSlotUtc);
        slotsAposConfirmar.Should().HaveCount(2);

        // 6) Cancelando, o slot volta — a vaga é devolvida.
        var cancelar = await treinador.PostAsJsonAsync($"/treinador/agenda/solicitacoes/{bookingRequestId}/cancelar", new { });
        cancelar.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(" || ", fixture.ErrosCapturados));

        var slotsAposCancelar = await ConsultarAvailabilityAsync(treinadorId, pacoteId, fromUtc, toUtc);
        slotsAposCancelar.Select(s => DateTimeOffset.Parse(s.GetProperty("startsAt").GetString()!).UtcDateTime)
            .Should().Contain(inicioSlotUtc);
        slotsAposCancelar.Should().HaveCount(3);
    }

    // --- Helpers de agenda/availability ---

    private static DateTime ProximaSegundaLocal()
    {
        var agoraLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FusoSaoPaulo);
        var diasAteSegunda = ((int)DayOfWeek.Monday - (int)agoraLocal.DayOfWeek + 7) % 7;
        diasAteSegunda = diasAteSegunda == 0 ? 7 : diasAteSegunda;
        return agoraLocal.Date.AddDays(diasAteSegunda);
    }

    private static string CaminhoBookingRequests(Guid tenantId) => $"{AgentEndpoints.Prefixo}/tenants/{tenantId}/booking-requests";

    private static string CaminhoAvailability(Guid tenantId, Guid serviceId, DateTime fromUtc, DateTime toUtc) =>
        $"{AgentEndpoints.Prefixo}/tenants/{tenantId}/availability?serviceId={serviceId}"
        + $"&from={Uri.EscapeDataString(fromUtc.ToString("O"))}&to={Uri.EscapeDataString(toUtc.ToString("O"))}";

    private async Task<List<JsonElement>> ConsultarAvailabilityAsync(Guid treinadorId, Guid pacoteId, DateTime fromUtc, DateTime toUtc)
    {
        using var resposta = await EnviarAssinadaAsync(
            fixture.CreateClient(), CaminhoAvailability(treinadorId, pacoteId, fromUtc, toUtc));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. corpo.RootElement.EnumerateArray().Select(e => e.Clone())];
    }

    private static async Task<HttpResponseMessage> EnviarAssinadaAsync(HttpClient cliente, string caminhoComQuery) =>
        await EnviarAssinadaAsync(cliente, HttpMethod.Get, caminhoComQuery, corpoJson: "");

    private static async Task<HttpResponseMessage> EnviarAssinadaAsync(HttpClient cliente, string caminho, string corpoJson) =>
        await EnviarAssinadaAsync(cliente, HttpMethod.Post, caminho, corpoJson);

    private static async Task<HttpResponseMessage> EnviarAssinadaAsync(
        HttpClient cliente, HttpMethod metodo, string caminho, string corpoJson)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var corpoBytes = metodo == HttpMethod.Get ? [] : Encoding.UTF8.GetBytes(corpoJson);
        var payload = $"{metodo.Method}\n{caminho}\n{Convert.ToHexStringLower(SHA256.HashData(corpoBytes))}\n{timestamp}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(RealPipelineFixture.AgentsHmacSecret), Encoding.UTF8.GetBytes(payload));

        using var requisicao = new HttpRequestMessage(metodo, caminho);
        if (metodo != HttpMethod.Get)
            requisicao.Content = new StringContent(corpoJson, Encoding.UTF8, "application/json");
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeAssinatura, "v1=" + Convert.ToHexStringLower(mac));
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeTimestamp, timestamp.ToString(provider: null));

        return await cliente.SendAsync(requisicao);
    }

    // --- Helpers de auth/gestão (mesmo padrão duplicado dos outros E2E — sem base compartilhada no repo) ---

    private async Task<Guid> TreinadorAprovadoAsync()
    {
        var treinadorId = await RegistrarTreinadorAsync();
        var admin = ClienteComToken(await LoginTokenAsync(RealPipelineFixture.AdminEmail, RealPipelineFixture.AdminPassword));

        using var req = new HttpRequestMessage(HttpMethod.Post, $"/admin/treinadores/{treinadorId}/aprovar")
        {
            Content = JsonContent.Create(new { }),
        };
        req.Headers.Add(RequerStepUpFilter.Header, await fixture.GerarStepUpTokenAsync(RealPipelineFixture.AdminEmail));
        (await admin.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.OK);

        return treinadorId;
    }

    private async Task<Guid> RegistrarTreinadorAsync()
    {
        var email = $"t{Guid.NewGuid():N}@e2e.test";
        var planos = await fixture.CreateClient().GetFromJsonAsync<JsonElement>("/auth/planos");
        var planoFreeId = planos.EnumerateArray().First(p => p.GetProperty("nome").GetString() == "Free").GetProperty("planoId").GetGuid();

        var response = await fixture.CreateClient().PostAsJsonAsync(
            "/auth/register/treinador",
            new { email, senha = SenhaPadrao, nome = "Treinador E2E Booking", planoPlataformaId = planoFreeId, modoPagamentoAluno = "Plataforma" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var treinadorId = body.GetProperty("treinadorId").GetGuid();
        _emailPorTreinador[treinadorId] = email;

        using var scope = fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaRepository>();
        var conta = await repo.ObterPorEmailAsync(email.Trim().ToLowerInvariant());
        conta!.MarcarEmailVerificado(DateTime.UtcNow);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();

        return treinadorId;
    }

    private async Task<string> LoginTokenAsync(Guid treinadorId) => await LoginTokenAsync(_emailPorTreinador[treinadorId], SenhaPadrao);

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
