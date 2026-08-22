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

// Caminho feliz + validação dos 2 GETs de leitura da fatia 1 do gateway de agentes, contra
// Postgres real (Testcontainers) e assinatura HMAC real — mesmo host que os fluxos críticos usam.
[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class AgentBusinessInfoAndServicesEndpointsTests(RealPipelineFixture fixture)
{
    private static async Task<Guid> SeedTreinadorPublicadoAsync(
        AppDbContext db,
        string nomeFantasia = "Studio Teste",
        params (string Categoria, decimal Preco, bool Trial)[] pacotesPublicos)
    {
        var email = Email.Criar($"treinador-{Guid.NewGuid():N}@teste.com").Value;
        var conta = Conta.Criar(email, "hash-bcrypt", TipoConta.Treinador, DateTime.UtcNow, emitirRegistro: false).Value;
        db.Contas.Add(conta);

        var treinador = Treinador.Criar(conta.Id, "Carlos Treinador", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.PerfilPublico.AtualizarDados(nomeFantasia, null, null, DateTime.UtcNow);
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);
        db.Treinadores.Add(treinador);

        foreach (var (categoria, preco, trial) in pacotesPublicos)
        {
            var pacote = Pacote.Criar(treinador.Id, "Pacote " + categoria, preco, DateTime.UtcNow).Value;
            pacote.AtualizarCatalogoPublico(categoria, 60, trial, DateTime.UtcNow);
            pacote.TornarPublico(DateTime.UtcNow);
            db.Pacotes.Add(pacote);
        }

        await db.SaveChangesAsync();
        return treinador.Id;
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

    private static string CaminhoBusinessInfo(Guid tenantId) => $"{AgentEndpoints.Prefixo}/tenants/{tenantId}/business-info";
    private static string CaminhoServices(Guid tenantId, string? category = null) =>
        category is null
            ? $"{AgentEndpoints.Prefixo}/tenants/{tenantId}/services"
            : $"{AgentEndpoints.Prefixo}/tenants/{tenantId}/services?category={Uri.EscapeDataString(category)}";

    [Fact]
    public async Task GetBusinessInfo_TenantPublicadoComCatalogo_Retorna200ComModalitiesDoCatalogo()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await SeedTreinadorPublicadoAsync(db, "Studio da Maria", ("Pilates", 150m, true));

        using var resposta = await EnviarAssinadaAsync(fixture.CreateClient(), CaminhoBusinessInfo(tenantId));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("name").GetString().Should().Be("Studio da Maria");
        corpo.RootElement.GetProperty("modalities").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(["Pilates"]);
        corpo.RootElement.TryGetProperty("plans", out _).Should().BeFalse();
        corpo.RootElement.TryGetProperty("address", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetBusinessInfo_TenantInexistente_Retorna404ComTenantNotFound()
    {
        using var resposta = await EnviarAssinadaAsync(fixture.CreateClient(), CaminhoBusinessInfo(Guid.NewGuid()));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be("tenant_not_found");
    }

    [Fact]
    public async Task GetBusinessInfo_TenantIdMalformado_Retorna400ComValidationFailed()
    {
        var caminho = $"{AgentEndpoints.Prefixo}/tenants/nao-e-um-guid/business-info";

        using var resposta = await EnviarAssinadaAsync(fixture.CreateClient(), caminho);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task GetServices_TenantPublicadoComCatalogo_Retorna200ComServicoMapeado()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await SeedTreinadorPublicadoAsync(db, "Studio Teste", ("Funcional", 199.90m, true));

        using var resposta = await EnviarAssinadaAsync(fixture.CreateClient(), CaminhoServices(tenantId));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var servico = corpo.RootElement.EnumerateArray().Single();
        servico.GetProperty("category").GetString().Should().Be("Funcional");
        servico.GetProperty("durationMinutes").GetInt32().Should().Be(60);
        servico.GetProperty("trialAvailable").GetBoolean().Should().BeTrue();
        servico.GetProperty("price").GetProperty("amount").GetDecimal().Should().Be(199.90m);
        servico.GetProperty("price").GetProperty("currency").GetString().Should().Be("BRL");
    }

    [Fact]
    public async Task GetServices_ComFiltroCategoryQueBate_Retorna200ComListaFiltrada()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await SeedTreinadorPublicadoAsync(
            db, "Studio Teste", ("Pilates", 150m, false), ("Funcional", 200m, false));

        using var resposta = await EnviarAssinadaAsync(fixture.CreateClient(), CaminhoServices(tenantId, "pilates"));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.EnumerateArray().Should().ContainSingle(e => e.GetProperty("category").GetString() == "Pilates");
    }

    [Fact]
    public async Task GetServices_TenantIdMalformado_Retorna400ComValidationFailed()
    {
        var caminho = $"{AgentEndpoints.Prefixo}/tenants/nao-e-um-guid/services";

        using var resposta = await EnviarAssinadaAsync(fixture.CreateClient(), caminho);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task GetServices_CategoryExcedeCemCaracteres_Retorna400ComValidationFailed()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await SeedTreinadorPublicadoAsync(db);
        var categoriaMuitoLonga = new string('a', 101);

        using var resposta = await EnviarAssinadaAsync(fixture.CreateClient(), CaminhoServices(tenantId, categoriaMuitoLonga));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("code").GetString().Should().Be("validation_failed");
    }
}
