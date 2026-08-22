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

// Fecha o gap achado pelo Verifier de agents-f1-perfil-publico: prova que o que o treinador
// grava via /treinador/perfil-publico e /treinador/pacotes (endpoints autenticados, de gestão)
// aparece de fato em GET business-info/services do gateway de agentes (leitura assinada por HMAC).
// Sem este teste, os testes de UI (que mockam a API) e os de agents (que seedam direto no banco)
// nunca provam essa ponta — era exatamente esse o buraco.
[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class PerfilPublicoWritePathE2ETests(RealPipelineFixture fixture)
{
    private const string SenhaPadrao = "SenhaForte123";
    private readonly Dictionary<Guid, string> _emailPorTreinador = new();

    [Fact]
    public async Task SalvarPerfilPublicoEPacotePublico_RefleteEmBusinessInfoEServices()
    {
        var treinadorId = await TreinadorAprovadoAsync();
        var treinador = ClienteComToken(await LoginTokenAsync(_emailPorTreinador[treinadorId]));

        var salvar = await treinador.PutAsJsonAsync("/treinador/perfil-publico", new
        {
            nomeFantasia = "Studio da Prova",
            endereco = (object?)null,
            politicas = (object?)null,
            horarios = Array.Empty<object>(),
            isPublicado = true,
        });
        salvar.StatusCode.Should().Be(HttpStatusCode.OK);

        var criarPacote = await treinador.PostAsJsonAsync("/treinador/pacotes", new
        {
            nome = "Pilates em grupo",
            preco = 180m,
            categoria = "Pilates",
            duracaoMinutos = 50,
            trialDisponivel = true,
            isPublico = true,
        });
        criarPacote.StatusCode.Should().Be(HttpStatusCode.Created);

        using var businessInfo = await EnviarAssinadaAsync(fixture.CreateClient(), CaminhoBusinessInfo(treinadorId));
        businessInfo.StatusCode.Should().Be(HttpStatusCode.OK);
        using var corpoInfo = JsonDocument.Parse(await businessInfo.Content.ReadAsStringAsync());
        corpoInfo.RootElement.GetProperty("name").GetString().Should().Be("Studio da Prova");
        corpoInfo.RootElement.GetProperty("modalities").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(["Pilates"]);

        using var services = await EnviarAssinadaAsync(fixture.CreateClient(), CaminhoServices(treinadorId));
        services.StatusCode.Should().Be(HttpStatusCode.OK);
        using var corpoServices = JsonDocument.Parse(await services.Content.ReadAsStringAsync());
        var servico = corpoServices.RootElement.EnumerateArray().Single();
        servico.GetProperty("category").GetString().Should().Be("Pilates");
        servico.GetProperty("durationMinutes").GetInt32().Should().Be(50);
        servico.GetProperty("trialAvailable").GetBoolean().Should().BeTrue();
        servico.GetProperty("price").GetProperty("amount").GetDecimal().Should().Be(180m);
    }

    [Fact]
    public async Task GetPerfilPublico_AposSalvar_DevolveOMesmoQueFoiEnviado()
    {
        var treinadorId = await TreinadorAprovadoAsync();
        var treinador = ClienteComToken(await LoginTokenAsync(_emailPorTreinador[treinadorId]));

        var salvar = await treinador.PutAsJsonAsync("/treinador/perfil-publico", new
        {
            nomeFantasia = "Studio Round Trip",
            endereco = new { rua = "Rua A", numero = "10", complemento = (string?)null, bairro = "Centro", cidade = "São Paulo", estado = "SP", cep = "01001000" },
            politicas = new Dictionary<string, string> { ["cancelamento"] = "24h de antecedência" },
            horarios = new[] { new { diaSemana = 1, abreAs = "08:00", fechaAs = "18:00" } },
            isPublicado = false,
        });
        salvar.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(" || ", fixture.ErrosCapturados));

        var lido = await treinador.GetFromJsonAsync<JsonElement>("/treinador/perfil-publico");

        lido.GetProperty("nomeFantasia").GetString().Should().Be("Studio Round Trip");
        lido.GetProperty("isPublicado").GetBoolean().Should().BeFalse();
        lido.GetProperty("endereco").GetProperty("cidade").GetString().Should().Be("São Paulo");
        lido.GetProperty("horariosFuncionamento").EnumerateArray().Should().ContainSingle(
            h => h.GetProperty("abreAs").GetString() == "08:00" && h.GetProperty("fechaAs").GetString() == "18:00");
        lido.GetProperty("politicas").GetProperty("cancelamento").GetString().Should().Be("24h de antecedência");
    }

    [Fact]
    public async Task SalvarPerfilPublico_PublicarSemNomeFantasia_Retorna422()
    {
        var treinadorId = await TreinadorAprovadoAsync();
        var treinador = ClienteComToken(await LoginTokenAsync(_emailPorTreinador[treinadorId]));

        var resposta = await treinador.PutAsJsonAsync("/treinador/perfil-publico", new
        {
            nomeFantasia = (string?)null,
            endereco = (object?)null,
            politicas = (object?)null,
            horarios = Array.Empty<object>(),
            isPublicado = true,
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SalvarPerfilPublico_HorarioComAbreDepoisDeFecha_Retorna422()
    {
        var treinadorId = await TreinadorAprovadoAsync();
        var treinador = ClienteComToken(await LoginTokenAsync(_emailPorTreinador[treinadorId]));

        var resposta = await treinador.PutAsJsonAsync("/treinador/perfil-publico", new
        {
            nomeFantasia = "Studio Inválido",
            endereco = (object?)null,
            politicas = (object?)null,
            horarios = new[] { new { diaSemana = 1, abreAs = "18:00", fechaAs = "08:00" } },
            isPublicado = false,
        });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- Helpers HMAC (assinatura real do grupo de agentes) ---

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
    private static string CaminhoServices(Guid tenantId) => $"{AgentEndpoints.Prefixo}/tenants/{tenantId}/services";

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
            new { email, senha = SenhaPadrao, nome = "Treinador E2E", planoPlataformaId = planoFreeId, modoPagamentoAluno = "Plataforma" });
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
