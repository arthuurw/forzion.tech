// F12 (Fase 3 test remediation) — concorrência em GerarCobrancaMensal.
//
// Sessão anterior (commit 18c3adc) envolveu a leitura+inserção de pagamento
// pendente numa transação serializable, mas o teste unit do handler usa um
// NoopTransaction — NÃO prova isolamento real. Aqui spawnamos 2 tarefas
// paralelas contra o MESMO assinaturaId via Postgres real (Testcontainers)
// e validamos que:
//   1) pelo menos uma tarefa retorna sucesso (idempotência ou criação inicial);
//   2) nunca há mais de 1 pagamento pendente persistido pra mesma assinatura;
//   3) se a 2a tarefa falhar, é com erro de serialização Postgres (40001),
//      NÃO com violação de constraint ou estado inconsistente.
//
// Sem a transação serializable, a janela entre leitura ("não há pendente")
// e inserção permitiria que ambas vissem null, ambas inserissem, e o índice
// parcial único quebraria a 2a com FK/conflict tardio — exatamente o que
// queríamos provar coberto.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace forzion.tech.Tests.E2E;

[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class ConcurrentBillingRaceTests(RealPipelineFixture fixture)
{
    private const string SenhaPadrao = "Senha@12345";

    [Fact]
    public async Task GerarCobrancaMensal_DuasChamadasParalelas_NaoCriaDuplicidade()
    {
        var (assinaturaId, treinadorId) = await CriarAssinaturaProntaAsync();

        var command = new GerarCobrancaMensalCommand(assinaturaId, treinadorId, MetodoPagamento.Pix);

        // Barrier sincroniza início — sem isso, uma task pode terminar antes da
        // outra começar e nunca disparar a race. Com barrier, ambas entram em
        // BeginTransactionAsync praticamente juntas.
        using var startBarrier = new Barrier(participantCount: 2);

        Task<HandlerOutcome> Run() => Task.Run(async () =>
        {
            startBarrier.SignalAndWait();
            using var scope = fixture.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<GerarCobrancaMensalHandler>();
            try
            {
                var result = await handler.HandleAsync(command);
                return new HandlerOutcome(Success: result.IsSuccess,
                    PagamentoId: result.IsSuccess ? result.Value.PagamentoId : null,
                    Exception: null);
            }
            catch (Exception ex)
            {
                return new HandlerOutcome(Success: false, PagamentoId: null, Exception: ex);
            }
        });

        var resultados = await Task.WhenAll(Run(), Run());

        // Pelo menos uma sucede (idempotente ou criação inicial). Sem proteção,
        // ambas falhariam tarde com violação de constraint OU ambas sucederiam
        // com 2 pagamentos órfãos — ambos cenários quebrariam invariantes.
        resultados.Count(r => r.Success).Should().BeGreaterThanOrEqualTo(1,
            "pelo menos uma das chamadas paralelas deve produzir um pagamento. Falhas: {0}",
            string.Join(" || ", resultados.Where(r => !r.Success).Select(FormatFalha)));

        // Falhas devem ser por race detectada (serialization failure 40001) —
        // qualquer outra exception indica gap no design da transação.
        foreach (var falha in resultados.Where(r => !r.Success && r.Exception is not null))
        {
            EhSerializationFailure(falha.Exception!).Should().BeTrue(
                "única falha aceitável é serialization failure 40001; recebi: {0}", falha.Exception);
        }

        // Invariante final: 0 ou 1 pagamento pendente persistido pra assinatura.
        // 2+ pendentes => proteção transacional falhou.
        using var queryScope = fixture.Services.CreateScope();
        var db = queryScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pendentes = await db.Pagamentos
            .Where(p => p.AssinaturaAlunoId == assinaturaId && p.Status == PagamentoStatus.Pendente)
            .CountAsync();
        pendentes.Should().BeLessThanOrEqualTo(1, "no máximo 1 pagamento pendente por assinatura");

        var totalSucessos = resultados.Count(r => r.Success);
        var idsRetornados = resultados.Where(r => r.Success).Select(r => r.PagamentoId).Distinct().Count();
        if (totalSucessos == 2)
        {
            idsRetornados.Should().Be(1,
                "ambas as chamadas que sucederam devem retornar o MESMO pagamento (idempotência via re-uso de pendente)");
        }
    }

    private record HandlerOutcome(bool Success, Guid? PagamentoId, Exception? Exception);

    private static string FormatFalha(HandlerOutcome r) =>
        r.Exception?.GetType().Name + ": " + (r.Exception?.Message ?? "<sem exception>");

    private static bool EhSerializationFailure(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur is PostgresException pg && pg.SqlState == "40001") return true;
        }
        return false;
    }

    // ─── Setup: cria treinador aprovado + onboarding + aluno + vinculo aprovado ─

    private async Task<(Guid assinaturaId, Guid treinadorId)> CriarAssinaturaProntaAsync()
    {
        var (treinadorId, _) = await TreinadorAprovadoComPlanoAsync();
        var treinador = await ClienteTreinadorAsync(treinadorId);

        await CompletarOnboardingAsync(treinador, treinadorId);

        var pacoteId = await CriarPacoteAsync(treinador);
        var (alunoId, _) = await RegistrarAlunoAsync(treinadorId, pacoteId);
        var vinculoId = await ObterVinculoPendenteIdAsync(alunoId);

        var aprovar = await treinador.PostAsJsonAsync(
            $"/treinador/vinculos/{vinculoId}/aprovar", new { pacoteId, trarFichas = false });
        aprovar.StatusCode.Should().Be(HttpStatusCode.OK);

        // VinculoAprovado → criar assinatura é handler DURÁVEL (outbox); materializa só ao drenar.
        await fixture.DrenarOutboxAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assinatura = await db.AssinaturaAlunos.FirstAsync(a => a.AlunoId == alunoId);
        return (assinatura.Id, treinadorId);
    }

    // ─── Helpers replicados de FluxosCriticosE2ETests (private → não compartilhável) ─

    private readonly Dictionary<Guid, string> _emailPorTreinador = new();

    private async Task<string> LoginTokenAsync(string email, string senha)
    {
        var response = await fixture.CreateClient().PostAsJsonAsync("/auth/login", new { email, senha });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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

    private async Task<HttpClient> ClienteTreinadorAsync(Guid treinadorId) =>
        ClienteComToken(await LoginTokenAsync(_emailPorTreinador[treinadorId], SenhaPadrao));

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
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "erro servidor: {0}", string.Join(" || ", fixture.ErrosCapturados));
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

        var status = await treinador.GetFromJsonAsync<JsonElement>("/treinador/onboarding/status");
        status.GetProperty("onboardingCompleto").GetBoolean().Should().BeTrue();
    }
}
