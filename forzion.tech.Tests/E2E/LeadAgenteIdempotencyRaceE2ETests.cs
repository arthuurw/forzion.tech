using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Api.Endpoints.Agents;
using forzion.tech.Api.Endpoints.Agents.Hmac;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.E2E;

// Corrida real de idempotência: duas requisições HTTP simultâneas, mesma idempotencyKey,
// mesmo Postgres real (Testcontainers) — prova que o índice único parcial (não um `if` de
// aplicação) resolve a corrida (TD-1/D9).
[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class LeadAgenteIdempotencyRaceE2ETests(RealPipelineFixture fixture)
{
    private static async Task<Guid> SeedTreinadorPublicadoAsync(AppDbContext db)
    {
        var email = Email.Criar($"treinador-{Guid.NewGuid():N}@teste.com").Value;
        var conta = Conta.Criar(email, "hash-bcrypt", TipoConta.Treinador, DateTime.UtcNow, emitirRegistro: false).Value;
        db.Contas.Add(conta);

        var treinador = Treinador.Criar(conta.Id, "Treinador Corrida", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.PerfilPublico.AtualizarDados("Studio Corrida", null, null, DateTime.UtcNow);
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);
        db.Treinadores.Add(treinador);

        await db.SaveChangesAsync();
        return treinador.Id;
    }

    private static string CaminhoLeads(Guid tenantId) => $"{AgentEndpoints.Prefixo}/tenants/{tenantId}/leads";

    private static async Task<HttpResponseMessage> EnviarAssinadaAsync(HttpClient cliente, string caminho, string corpoJson)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"POST\n{caminho}\n{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(corpoJson)))}\n{timestamp}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(RealPipelineFixture.AgentsHmacSecret), Encoding.UTF8.GetBytes(payload));

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, caminho)
        {
            Content = new StringContent(corpoJson, Encoding.UTF8, "application/json")
        };
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeAssinatura, "v1=" + Convert.ToHexStringLower(mac));
        requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeTimestamp, timestamp.ToString(provider: null));

        return await cliente.SendAsync(requisicao);
    }

    [Fact]
    public async Task DoisPostsSimultaneosMesmaIdempotencyKey_ExatamenteUmaLinhaEMesmoLeadIdNasDuasRespostas()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await SeedTreinadorPublicadoAsync(db);
        var caminho = CaminhoLeads(tenantId);
        const string corpo =
            """{"name":"Corredor","contact":{"type":"email","value":"corredor@lead.com"},"consent":{"granted":true,"purpose":"Contato comercial"},"idempotencyKey":"chave-corrida-e2e"}""";

        using var barreira = new Barrier(participantCount: 2);

        Task<HttpResponseMessage> DispararAsync() => Task.Run(async () =>
        {
            barreira.SignalAndWait();
            return await EnviarAssinadaAsync(fixture.CreateClient(), caminho, corpo);
        });

        var todas = await Task.WhenAll(DispararAsync(), DispararAsync());
        try
        {
            var leadIds = new List<string>();
            foreach (var r in todas)
            {
                r.StatusCode.Should().Be(HttpStatusCode.Created);
                using var corpoResposta = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
                leadIds.Add(corpoResposta.RootElement.GetProperty("leadId").GetString()!);
            }

            leadIds.Distinct().Should().ContainSingle("as duas respostas devolvem o MESMO leadId");
        }
        finally
        {
            foreach (var r in todas)
                r.Dispose();
        }

        using var verificacaoScope = fixture.Services.CreateScope();
        var verificacaoDb = verificacaoScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var total = await verificacaoDb.Leads.CountAsync(l => l.TreinadorId == tenantId);
        total.Should().Be(1, "exatamente 1 linha apesar das duas requisições concorrentes");
    }
}
