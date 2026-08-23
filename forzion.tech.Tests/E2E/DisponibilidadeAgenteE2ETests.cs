using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Api.Endpoints.Agents;
using forzion.tech.Api.Endpoints.Agents.Hmac;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Services;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.E2E;

// Caminho do agente consultando disponibilidade — HMAC real + Postgres real (Testcontainers),
// mesmo host que LeadAgenteE2ETests (fatia 2). Independent Test do spec.md: treinador publicado,
// pacote público de 60min, horário seg 08:00-11:00 -> 3 slots (08,09,10) em UTC para
// America/Sao_Paulo.
[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class DisponibilidadeAgenteE2ETests(RealPipelineFixture fixture)
{
    private static readonly TimeZoneInfo FusoSaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private static async Task<(Guid TreinadorId, Guid PacoteId, DateTime SegundaLocal)> SeedTreinadorComAgendaDeSegundaAsync(
        AppDbContext db, int duracaoMinutos = 60, int capacidadeMaxima = 2)
    {
        var email = Email.Criar($"treinador-{Guid.NewGuid():N}@teste.com").Value;
        var conta = Conta.Criar(email, "hash-bcrypt", TipoConta.Treinador, DateTime.UtcNow, emitirRegistro: false).Value;
        db.Contas.Add(conta);

        var treinador = Treinador.Criar(conta.Id, "Treinador Disponibilidade", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.DefinirFusoHorario("America/Sao_Paulo", DateTime.UtcNow);
        treinador.PerfilPublico.AtualizarDados("Studio Disponibilidade", null, null, DateTime.UtcNow);
        var segundaLocal = ProximaSegundaLocal();
        treinador.PerfilPublico.AdicionarHorario((int)DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(11, 0), DateTime.UtcNow);
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);
        db.Treinadores.Add(treinador);

        var pacote = Pacote.Criar(treinador.Id, "Sessão de treino", 100m, DateTime.UtcNow).Value;
        pacote.AtualizarCatalogoPublico("Treino", duracaoMinutos, false, DateTime.UtcNow, capacidadeMaxima);
        pacote.TornarPublico(DateTime.UtcNow);
        db.Pacotes.Add(pacote);

        await db.SaveChangesAsync();
        return (treinador.Id, pacote.Id, segundaLocal);
    }

    // Segunda-feira futura o suficiente p/ nunca colidir com a antecedência mínima padrão (2h):
    // sempre pelo menos 1 dia de folga, calculado no fuso do treinador (não em UTC) p/ evitar
    // deriva perto da meia-noite.
    private static DateTime ProximaSegundaLocal()
    {
        var agoraLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FusoSaoPaulo);
        var diasAteSegunda = ((int)DayOfWeek.Monday - (int)agoraLocal.DayOfWeek + 7) % 7;
        diasAteSegunda = diasAteSegunda == 0 ? 7 : diasAteSegunda;
        return agoraLocal.Date.AddDays(diasAteSegunda);
    }

    private static DateTime ParaUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), FusoSaoPaulo);

    private static string CaminhoAvailability(Guid tenantId, Guid serviceId, DateTime fromUtc, DateTime toUtc) =>
        $"{AgentEndpoints.Prefixo}/tenants/{tenantId}/availability?serviceId={serviceId}&from={Uri.EscapeDataString(fromUtc.ToString("O"))}&to={Uri.EscapeDataString(toUtc.ToString("O"))}";

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

    [Fact]
    public async Task TreinadorPublicadoComPacoteEHorario_PostAssinado_DevolveTresSlotsComInstantesUtcExatos()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (treinadorId, pacoteId, segundaLocal) = await SeedTreinadorComAgendaDeSegundaAsync(db);

        var fromUtc = ParaUtc(segundaLocal);
        var toUtc = ParaUtc(segundaLocal.AddDays(1));

        using var resposta = await EnviarAssinadaAsync(fixture.CreateClient(), CaminhoAvailability(treinadorId, pacoteId, fromUtc, toUtc));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var slots = corpo.RootElement.EnumerateArray().ToList();
        slots.Should().HaveCount(3);

        var inicio08Utc = ParaUtc(segundaLocal.AddHours(8));
        var instantesEsperados = new[] { inicio08Utc, inicio08Utc.AddHours(1), inicio08Utc.AddHours(2) };

        for (var i = 0; i < 3; i++)
        {
            slots[i].GetProperty("slotId").GetString().Should().Be(SlotId.Calcular(treinadorId, pacoteId, instantesEsperados[i]));
            slots[i].GetProperty("serviceId").GetString().Should().Be(pacoteId.ToString());
            DateTimeOffset.Parse(slots[i].GetProperty("startsAt").GetString()!).UtcDateTime.Should().Be(instantesEsperados[i]);
            DateTimeOffset.Parse(slots[i].GetProperty("endsAt").GetString()!).UtcDateTime.Should().Be(instantesEsperados[i].AddHours(1));
            slots[i].GetProperty("capacityRemaining").GetInt32().Should().Be(2);
        }
    }

    [Fact]
    public async Task TenantInexistente_Retorna404TenantNotFound()
    {
        var tenantInexistente = Guid.NewGuid();
        var fromUtc = ParaUtc(ProximaSegundaLocal());

        using var resposta = await EnviarAssinadaAsync(
            fixture.CreateClient(), CaminhoAvailability(tenantInexistente, Guid.NewGuid(), fromUtc, fromUtc.AddDays(1)));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be("tenant_not_found");
    }

    [Fact]
    public async Task ServicoInexistente_Retorna404ServiceNotFoundDistintoDeTenantNotFound()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (treinadorId, _, segundaLocal) = await SeedTreinadorComAgendaDeSegundaAsync(db);
        var fromUtc = ParaUtc(segundaLocal);

        using var resposta = await EnviarAssinadaAsync(
            fixture.CreateClient(), CaminhoAvailability(treinadorId, Guid.NewGuid(), fromUtc, fromUtc.AddDays(1)));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be("service_not_found");
    }
}
