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
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.E2E;

// Fecha o mesmo gap que AD-018 (fatia 1): prova que o que o treinador grava por HTTP real em
// /treinador/agenda/* e /treinador/perfil-publico muda de fato o que a MESMA chamada
// HMAC-assinada de availability devolve ao agente. Sem mock em nenhuma ponta — nem repositório
// fake, nem handler chamado direto. Molde: PerfilPublicoWritePathE2ETests.cs.
[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class AgendaWritePathE2ETests(RealPipelineFixture fixture)
{
    private const string SenhaPadrao = "SenhaForte123";
    private static readonly TimeZoneInfo FusoSaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly TimeZoneInfo FusoManaus = TimeZoneInfo.FindSystemTimeZoneById("America/Manaus");

    [Fact]
    public async Task TreinadorGerenciaAgendaPorHttpReal_ChamadaHmacDeAvailabilityRefleteCadaMudanca()
    {
        var treinadorId = await TreinadorAprovadoAsync();
        var treinador = ClienteComToken(await LoginTokenAsync(treinadorId));
        var segundaLocal = ProximaSegundaLocal();

        var salvarPerfil = await treinador.PutAsJsonAsync("/treinador/perfil-publico", new
        {
            nomeFantasia = "Studio Cadeia Real",
            endereco = (object?)null,
            politicas = (object?)null,
            horarios = new[] { new { diaSemana = 1, abreAs = "08:00", fechaAs = "11:00" } },
            isPublicado = true,
            fusoHorario = "America/Sao_Paulo",
        });
        salvarPerfil.StatusCode.Should().Be(HttpStatusCode.OK);

        var criarPacote = await treinador.PostAsJsonAsync("/treinador/pacotes", new
        {
            nome = "Sessão avulsa",
            preco = 150m,
            categoria = "Treino",
            duracaoMinutos = 60,
            trialDisponivel = false,
            isPublico = true,
        });
        criarPacote.StatusCode.Should().Be(HttpStatusCode.Created);
        var pacoteId = (await criarPacote.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("pacoteId").GetGuid();

        var fromUtc = DateTime.SpecifyKind(segundaLocal.AddDays(-1), DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(segundaLocal.AddDays(2), DateTimeKind.Utc);
        var instante08SaoPauloUtc = ParaUtc(segundaLocal.AddHours(8), FusoSaoPaulo);
        var instante09SaoPauloUtc = instante08SaoPauloUtc.AddHours(1);
        var instante10SaoPauloUtc = instante08SaoPauloUtc.AddHours(2);

        // 1) Estado inicial: sem bloqueio, os 3 slots aparecem.
        var slotsIniciais = await ConsultarSlotsAsync(treinadorId, pacoteId, fromUtc, toUtc);
        slotsIniciais.Should().BeEquivalentTo([instante08SaoPauloUtc, instante09SaoPauloUtc, instante10SaoPauloUtc]);

        // 2) Treinador cria bloqueio recorrente cobrindo o slot das 09h; a MESMA consulta assinada
        // deixa de trazê-lo.
        var criarBloqueio = await treinador.PostAsJsonAsync("/treinador/agenda/bloqueios", new
        {
            tipo = "RecorrenteSemanal",
            diaSemana = 1,
            horaInicio = "09:00",
            horaFim = "10:00",
        });
        criarBloqueio.StatusCode.Should().Be(HttpStatusCode.Created);
        var bloqueioId = (await criarBloqueio.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var slotsComBloqueio = await ConsultarSlotsAsync(treinadorId, pacoteId, fromUtc, toUtc);
        slotsComBloqueio.Should().BeEquivalentTo([instante08SaoPauloUtc, instante10SaoPauloUtc]);

        // 3) Treinador apaga o bloqueio; o slot das 09h volta.
        var apagarBloqueio = await treinador.DeleteAsync($"/treinador/agenda/bloqueios/{bloqueioId}");
        apagarBloqueio.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var slotsAposApagar = await ConsultarSlotsAsync(treinadorId, pacoteId, fromUtc, toUtc);
        slotsAposApagar.Should().BeEquivalentTo([instante08SaoPauloUtc, instante09SaoPauloUtc, instante10SaoPauloUtc]);

        // 4) Treinador troca o fuso (mesmos horários locais); os instantes UTC dos slots mudam.
        var trocarFuso = await treinador.PutAsJsonAsync("/treinador/perfil-publico", new
        {
            nomeFantasia = "Studio Cadeia Real",
            endereco = (object?)null,
            politicas = (object?)null,
            horarios = new[] { new { diaSemana = 1, abreAs = "08:00", fechaAs = "11:00" } },
            isPublicado = true,
            fusoHorario = "America/Manaus",
        });
        trocarFuso.StatusCode.Should().Be(HttpStatusCode.OK);

        var instante08ManausUtc = ParaUtc(segundaLocal.AddHours(8), FusoManaus);
        var slotsAposTrocarFuso = await ConsultarSlotsAsync(treinadorId, pacoteId, fromUtc, toUtc);
        slotsAposTrocarFuso.Should().BeEquivalentTo(
            [instante08ManausUtc, instante08ManausUtc.AddHours(1), instante08ManausUtc.AddHours(2)]);
        slotsAposTrocarFuso.Should().NotContain(instante08SaoPauloUtc, "o mesmo wall-clock agora vale um instante UTC diferente");
    }

    // --- Helpers de agenda/availability ---

    private static DateTime ProximaSegundaLocal()
    {
        var agoraLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FusoSaoPaulo);
        var diasAteSegunda = ((int)DayOfWeek.Monday - (int)agoraLocal.DayOfWeek + 7) % 7;
        diasAteSegunda = diasAteSegunda == 0 ? 7 : diasAteSegunda;
        return agoraLocal.Date.AddDays(diasAteSegunda);
    }

    private static DateTime ParaUtc(DateTime local, TimeZoneInfo fuso) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), fuso);

    private async Task<List<DateTime>> ConsultarSlotsAsync(Guid treinadorId, Guid pacoteId, DateTime fromUtc, DateTime toUtc)
    {
        var caminho = $"{AgentEndpoints.Prefixo}/tenants/{treinadorId}/availability?serviceId={pacoteId}"
            + $"&from={Uri.EscapeDataString(fromUtc.ToString("O"))}&to={Uri.EscapeDataString(toUtc.ToString("O"))}";

        using var resposta = await EnviarAssinadaAsync(fixture.CreateClient(), caminho);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. corpo.RootElement.EnumerateArray()
            .Select(s => DateTimeOffset.Parse(s.GetProperty("startsAt").GetString()!).UtcDateTime)];
    }

    private static async Task<HttpResponseMessage> EnviarAssinadaAsync(HttpClient cliente, string caminhoComQuery)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"GET\n{caminhoComQuery}\n{Convert.ToHexStringLower(SHA256.HashData([]))}\n{timestamp}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(RealPipelineFixture.AgentsHmacSecret), Encoding.UTF8.GetBytes(payload));

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, caminhoComQuery);
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeAssinatura, "v1=" + Convert.ToHexStringLower(mac));
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeTimestamp, timestamp.ToString(provider: null));

        return await cliente.SendAsync(requisicao);
    }

    // --- Helpers de auth/gestão (mesmo padrão duplicado dos outros E2E — sem base compartilhada no repo) ---

    private readonly Dictionary<Guid, string> _emailPorTreinador = new();

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
            new { email, senha = SenhaPadrao, nome = "Treinador E2E Agenda", planoPlataformaId = planoFreeId, modoPagamentoAluno = "Plataforma" });
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
