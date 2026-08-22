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

// Caminho do agente registrando lead — HMAC real + Postgres real (Testcontainers), mesmo host
// que os demais fluxos críticos e a fatia 1 de leitura do gateway de agentes.
[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class LeadAgenteE2ETests(RealPipelineFixture fixture)
{
    private static async Task<Guid> SeedTreinadorPublicadoAsync(AppDbContext db)
    {
        var email = Email.Criar($"treinador-{Guid.NewGuid():N}@teste.com").Value;
        var conta = Conta.Criar(email, "hash-bcrypt", TipoConta.Treinador, DateTime.UtcNow, emitirRegistro: false).Value;
        db.Contas.Add(conta);

        var treinador = Treinador.Criar(conta.Id, "Treinador Publicado", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.PerfilPublico.AtualizarDados("Studio Publicado", null, null, DateTime.UtcNow);
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);
        db.Treinadores.Add(treinador);

        await db.SaveChangesAsync();
        return treinador.Id;
    }

    private static async Task<Guid> SeedTreinadorNaoPublicadoAsync(AppDbContext db)
    {
        var email = Email.Criar($"treinador-{Guid.NewGuid():N}@teste.com").Value;
        var conta = Conta.Criar(email, "hash-bcrypt", TipoConta.Treinador, DateTime.UtcNow, emitirRegistro: false).Value;
        db.Contas.Add(conta);

        var treinador = Treinador.Criar(conta.Id, "Treinador Nao Publicado", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
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

    private static string CorpoValido(string idempotencyKey, string nome = "Fulano", string interesse = "quero treinar") =>
        $$"""{"name":"{{nome}}","contact":{"type":"email","value":"fulano@lead.com"},"interest":"{{interesse}}","consent":{"granted":true,"purpose":"Contato comercial"},"idempotencyKey":"{{idempotencyKey}}"}""";

    [Fact]
    public async Task TreinadorPublicado_PostAssinado_Retorna201EPersisteLeadDeAgentePendente()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await SeedTreinadorPublicadoAsync(db);
        var idempotencyKey = $"chave-{Guid.NewGuid():N}";

        using var resposta = await EnviarAssinadaAsync(fixture.CreateClient(), CaminhoLeads(tenantId), CorpoValido(idempotencyKey));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var texto = await resposta.Content.ReadAsStringAsync();
        using var corpo = JsonDocument.Parse(texto);
        corpo.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["leadId", "source", "status"]);
        corpo.RootElement.GetProperty("source").GetString().Should().Be("agent");
        corpo.RootElement.GetProperty("status").GetString().Should().Be("pending");
        var leadId = Guid.Parse(corpo.RootElement.GetProperty("leadId").GetString()!);

        using var verificacaoScope = fixture.Services.CreateScope();
        var verificacaoDb = verificacaoScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lead = await verificacaoDb.Leads.SingleAsync(l => l.Id == leadId);
        lead.Source.Should().Be(LeadSource.Agent);
        lead.Status.Should().Be(LeadStatus.Novo);
        lead.TreinadorId.Should().Be(tenantId);
    }

    [Fact]
    public async Task TreinadorNaoPublicado_Retorna404IdenticoAoDeInexistente()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantNaoPublicado = await SeedTreinadorNaoPublicadoAsync(db);
        var tenantInexistente = Guid.NewGuid();

        using var respostaNaoPublicado = await EnviarAssinadaAsync(
            fixture.CreateClient(), CaminhoLeads(tenantNaoPublicado), CorpoValido($"chave-{Guid.NewGuid():N}"));
        using var respostaInexistente = await EnviarAssinadaAsync(
            fixture.CreateClient(), CaminhoLeads(tenantInexistente), CorpoValido($"chave-{Guid.NewGuid():N}"));

        respostaNaoPublicado.StatusCode.Should().Be(HttpStatusCode.NotFound);
        respostaInexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var corpoNaoPublicado = JsonDocument.Parse(await respostaNaoPublicado.Content.ReadAsStringAsync());
        using var corpoInexistente = JsonDocument.Parse(await respostaInexistente.Content.ReadAsStringAsync());
        corpoNaoPublicado.RootElement.GetProperty("code").GetString().Should().Be("tenant_not_found");
        corpoNaoPublicado.RootElement.GetProperty("detail").GetString()
            .Should().Be(corpoInexistente.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ConsentimentoNaoConcedido_Retorna400EZeroLinhaNaTabela()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await SeedTreinadorPublicadoAsync(db);
        var corpo =
            $$"""{"name":"Fulano","contact":{"type":"email","value":"fulano@lead.com"},"consent":{"granted":false,"purpose":"Contato comercial"},"idempotencyKey":"chave-{{Guid.NewGuid():N}}"}""";

        using var resposta = await EnviarAssinadaAsync(fixture.CreateClient(), CaminhoLeads(tenantId), corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var corpoResposta = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpoResposta.RootElement.GetProperty("code").GetString().Should().Be("validation_failed");

        using var verificacaoScope = fixture.Services.CreateScope();
        var verificacaoDb = verificacaoScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verificacaoDb.Leads.CountAsync(l => l.TreinadorId == tenantId)).Should().Be(0);
    }

    [Fact]
    public async Task MesmaChaveArgumentosDiferentes_Retorna409()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await SeedTreinadorPublicadoAsync(db);
        var idempotencyKey = $"chave-{Guid.NewGuid():N}";
        var cliente = fixture.CreateClient();

        using var primeira = await EnviarAssinadaAsync(cliente, CaminhoLeads(tenantId), CorpoValido(idempotencyKey, nome: "Fulano"));
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);

        using var segunda = await EnviarAssinadaAsync(cliente, CaminhoLeads(tenantId), CorpoValido(idempotencyKey, nome: "Outro Nome"));

        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var corpoResposta = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync());
        corpoResposta.RootElement.GetProperty("code").GetString().Should().Be("idempotency_conflict");

        using var verificacaoScope = fixture.Services.CreateScope();
        var verificacaoDb = verificacaoScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verificacaoDb.Leads.CountAsync(l => l.TreinadorId == tenantId)).Should().Be(1);
    }
}
