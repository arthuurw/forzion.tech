using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.E2E;

// Fluxos ponta-a-ponta com handlers/infra REAIS contra Postgres real (Testcontainers).
// Só o Stripe é fake. Cada teste usa e-mails únicos (Guid) — isolamento por dado,
// já que a fixture/DB são compartilhados pela coleção.
[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class FluxosCriticosE2ETests(RealPipelineFixture fixture)
{
    private const string SenhaPadrao = "Senha@12345";

    // --- Fluxo 1: cadastro de treinador → login admin → aprovação ---

    [Fact]
    public async Task Fluxo_CadastroTreinador_AprovadoPeloAdmin()
    {
        var (treinadorId, _) = await RegistrarTreinadorAsync();

        var admin = await ClienteAdminAsync();
        await AprovarTreinadorAdminAsync(admin, treinadorId);

        var detalhe = await admin.GetFromJsonAsync<JsonElement>($"/admin/treinadores/{treinadorId}");
        detalhe.GetProperty("status").GetString().Should().Be("Ativo");
    }

    // --- Fluxo 2: cadastro de aluno → vínculo pendente persistido ---

    [Fact]
    public async Task Fluxo_CadastroAluno_CriaVinculoPendente()
    {
        var (treinadorId, _) = await TreinadorAprovadoComPlanoAsync();
        var treinador = await ClienteTreinadorAsync(treinadorId);
        var pacoteId = await CriarPacoteAsync(treinador);

        var (alunoId, _) = await RegistrarAlunoAsync(treinadorId, pacoteId);

        var admin = await ClienteAdminAsync();
        var vinculo = await admin.GetFromJsonAsync<JsonElement>($"/admin/alunos/{alunoId}/vinculo");
        vinculo.GetProperty("vinculoPendente").ValueKind.Should().Be(JsonValueKind.Object);
        vinculo.GetProperty("vinculoPendente").GetProperty("status").GetString().Should().Be("AguardandoAprovacao");
    }

    // --- Fluxo 3: aprovação de vínculo → criação de AssinaturaAluno (persistida) ---

    [Fact]
    public async Task Fluxo_AprovacaoVinculo_CriaAssinaturaAluno()
    {
        var (treinadorId, treinadorEmail) = await TreinadorAprovadoComPlanoAsync();
        var treinador = await ClienteTreinadorAsync(treinadorId);

        // Onboarding Stripe (fake) — sem ele o handler de evento não cria a assinatura.
        await CompletarOnboardingAsync(treinador, treinadorId);

        var pacoteId = await CriarPacoteAsync(treinador);
        var (alunoId, _) = await RegistrarAlunoAsync(treinadorId, pacoteId);
        var vinculoId = await ObterVinculoPendenteIdAsync(alunoId);

        var aprovar = await treinador.PostAsJsonAsync(
            $"/treinador/vinculos/{vinculoId}/aprovar", new { pacoteId, trarFichas = false });
        aprovar.StatusCode.Should().Be(HttpStatusCode.OK);

        // VinculoAprovado → criar assinatura é handler DURÁVEL (outbox); materializa só ao drenar.
        await fixture.DrenarOutboxAsync();

        // Assinatura criada via domain event handler real, persistida no banco real.
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assinatura = await db.AssinaturaAlunos.FirstOrDefaultAsync(a => a.AlunoId == alunoId);

        assinatura.Should().NotBeNull("o vínculo aprovado com onboarding completo deve gerar AssinaturaAluno");
        assinatura!.TreinadorId.Should().Be(treinadorId);
        assinatura.PacoteId.Should().Be(pacoteId);
    }

    // --- Helpers ---

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

    private async Task<HttpClient> ClienteAdminAsync() =>
        ClienteComToken(await LoginTokenAsync(RealPipelineFixture.AdminEmail, RealPipelineFixture.AdminPassword));

    private async Task AprovarTreinadorAdminAsync(HttpClient admin, Guid treinadorId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/admin/treinadores/{treinadorId}/aprovar")
        {
            Content = JsonContent.Create(new { }),
        };
        req.Headers.Add(RequerStepUpFilter.Header, await fixture.GerarStepUpTokenAsync(RealPipelineFixture.AdminEmail));
        (await admin.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpClient> ClienteTreinadorAsync(Guid treinadorId)
    {
        // o e-mail do treinador é determinístico a partir do id criado em RegistrarTreinadorAsync
        var email = _emailPorTreinador[treinadorId];
        return ClienteComToken(await LoginTokenAsync(email, SenhaPadrao));
    }

    private readonly Dictionary<Guid, string> _emailPorTreinador = new();

    private async Task<Guid> ObterPlanoFreeIdAsync()
    {
        var planos = await fixture.CreateClient().GetFromJsonAsync<JsonElement>("/auth/planos");
        return planos.EnumerateArray()
            .First(p => p.GetProperty("nome").GetString() == "Free")
            .GetProperty("planoId").GetGuid();
    }

    private async Task<(Guid treinadorId, string email)> RegistrarTreinadorAsync()
    {
        var email = $"t{Guid.NewGuid():N}@e2e.test";
        var planoFreeId = await ObterPlanoFreeIdAsync();
        var response = await fixture.CreateClient().PostAsJsonAsync(
            "/auth/register/treinador",
            new { email, senha = SenhaPadrao, nome = "Treinador E2E", planoPlataformaId = planoFreeId, modoPagamentoAluno = "Plataforma" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var treinadorId = body.GetProperty("treinadorId").GetGuid();
        _emailPorTreinador[treinadorId] = email;
        // Resend usa NullEmailService nos testes: o token de verificação só existiria
        // no e-mail. Marca a conta como verificada direto no banco para liberar o login.
        await VerificarEmailDiretoAsync(email);
        return (treinadorId, email);
    }

    private async Task VerificarEmailDiretoAsync(string email)
    {
        using var scope = fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaRepository>();
        var conta = await repo.ObterPorEmailAsync(email.Trim().ToLowerInvariant());
        conta!.MarcarEmailVerificado(DateTime.UtcNow);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
    }

    private async Task<(Guid treinadorId, string email)> TreinadorAprovadoComPlanoAsync()
    {
        var (treinadorId, email) = await RegistrarTreinadorAsync();
        var admin = await ClienteAdminAsync();

        await AprovarTreinadorAdminAsync(admin, treinadorId);

        var freeId = await ObterPlanoFreeIdAsync();
        (await admin.PatchAsJsonAsync($"/admin/treinadores/{treinadorId}/plano", new { planoId = freeId }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        return (treinadorId, email);
    }

    private static async Task<Guid> CriarPacoteAsync(HttpClient treinador)
    {
        var response = await treinador.PostAsJsonAsync(
            "/treinador/pacotes", new { nome = "Pacote E2E", preco = 199.90m, descricao = "Pacote de teste" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("pacoteId").GetGuid();
    }

    private async Task<(Guid alunoId, string email)> RegistrarAlunoAsync(Guid treinadorId, Guid pacoteId)
    {
        var email = $"a{Guid.NewGuid():N}@e2e.test";
        var response = await fixture.CreateClient().PostAsJsonAsync(
            "/auth/register/aluno", new { email, senha = SenhaPadrao, nome = "Aluno E2E", treinadorId, pacoteId });
        var diag = string.Join(" || ", fixture.ErrosCapturados);
        response.StatusCode.Should().Be(HttpStatusCode.Created, "erro servidor: {0}", diag);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("alunoId").GetGuid(), email);
    }

    private async Task<Guid> ObterVinculoPendenteIdAsync(Guid alunoId)
    {
        var admin = await ClienteAdminAsync();
        var vinculo = await admin.GetFromJsonAsync<JsonElement>($"/admin/alunos/{alunoId}/vinculo");
        return vinculo.GetProperty("vinculoPendente").GetProperty("vinculoId").GetGuid();
    }

    private async Task CompletarOnboardingAsync(HttpClient treinador, Guid treinadorId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/treinador/onboarding")
        {
            Content = JsonContent.Create(new
            {
                urlRetorno = $"{RealPipelineFixture.UrlBase}/retorno",
                urlCancelamento = $"{RealPipelineFixture.UrlBase}/cancelar"
            }),
        };
        req.Headers.Add(RequerStepUpFilter.Header, await fixture.GerarStepUpTokenAsync(_emailPorTreinador[treinadorId]));
        (await treinador.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.OK);

        // O fake Stripe reporta conta ativada → ConfirmarOnboarding marca OnboardingCompleto.
        var status = await treinador.GetFromJsonAsync<JsonElement>("/treinador/onboarding/status");
        status.GetProperty("onboardingCompleto").GetBoolean().Should().BeTrue();
    }
}
