using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using forzion.tech.Application.UseCases.Treinadores.GerarCobrancaPlanoTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        using var startBarrier = new Barrier(participantCount: 2);
        var intentsAntes = fixture.Stripe.PaymentIntentsCriados;

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

        resultados.Should().OnlyContain(r => r.Success,
            "com retry de 40001 ambas as chamadas concorrentes concluem (a perdedora reusa o pendente da vencedora). Falhas: {0}",
            string.Join(" || ", resultados.Where(r => !r.Success).Select(FormatFalha)));

        resultados.Select(r => r.PagamentoId).Distinct().Should().HaveCount(1,
            "ambas retornam o MESMO pagamento (idempotência via re-uso do pendente)");

        (fixture.Stripe.PaymentIntentsCriados - intentsAntes).Should().Be(1,
            "idem-key determinística (mesma assinatura + bucket de minuto) ⇒ um único PaymentIntent no Stripe");

        using var queryScope = fixture.Services.CreateScope();
        var db = queryScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pendentes = await db.Pagamentos
            .Where(p => p.AssinaturaAlunoId == assinaturaId && p.Status == PagamentoStatus.Pendente)
            .CountAsync();
        pendentes.Should().Be(1, "exatamente 1 pagamento pendente por assinatura");
    }

    [Fact]
    public async Task GerarCobrancaPlanoTreinador_DuasChamadasParalelas_NaoCriaDuplicidade()
    {
        var assinaturaId = await CriarAssinaturaTreinadorAtivaAsync();

        var command = new GerarCobrancaPlanoTreinadorCommand(assinaturaId, MetodoPagamento.Pix);

        using var startBarrier = new Barrier(participantCount: 2);
        var intentsAntes = fixture.Stripe.PaymentIntentsCriados;

        Task<HandlerOutcome> Run() => Task.Run(async () =>
        {
            startBarrier.SignalAndWait();
            using var scope = fixture.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<GerarCobrancaPlanoTreinadorHandler>();
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

        resultados.Should().OnlyContain(r => r.Success,
            "com retry de 40001 ambas as chamadas concorrentes concluem. Falhas: {0}",
            string.Join(" || ", resultados.Where(r => !r.Success).Select(FormatFalha)));

        resultados.Select(r => r.PagamentoId).Distinct().Should().HaveCount(1,
            "ambas retornam o MESMO pagamento de renovação (idempotência via re-uso do pendente)");

        (fixture.Stripe.PaymentIntentsCriados - intentsAntes).Should().Be(1,
            "idem-key determinística (mesma assinatura + bucket de minuto) ⇒ um único PaymentIntent no Stripe");

        using var queryScope = fixture.Services.CreateScope();
        var db = queryScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pendentes = await db.PagamentosTreinador
            .Where(p => p.AssinaturaTreinadorId == assinaturaId && p.Status == PagamentoStatus.Pendente)
            .CountAsync();
        pendentes.Should().Be(1, "exatamente 1 pagamento pendente por assinatura de treinador");
    }

    [Fact]
    public async Task ProcessarWebhookPago_DuasEntregasParalelas_AplicaEfeitoUnicoSem500()
    {
        var (assinaturaId, treinadorId) = await CriarAssinaturaProntaAsync();

        string paymentIntentId;
        using (var scope = fixture.Services.CreateScope())
        {
            var cobranca = scope.ServiceProvider.GetRequiredService<GerarCobrancaMensalHandler>();
            var gerar = await cobranca.HandleAsync(new GerarCobrancaMensalCommand(assinaturaId, treinadorId, MetodoPagamento.Pix));
            gerar.IsSuccess.Should().BeTrue();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pendente = await db.Pagamentos.FirstAsync(p => p.AssinaturaAlunoId == assinaturaId && p.Status == PagamentoStatus.Pendente);
            paymentIntentId = pendente.StripePaymentIntentId!;
        }

        var payload = "{\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"id\":\"" + paymentIntentId + "\"}}}";
        var evento = StripeWebhookParser.Parse(payload);

        using var startBarrier = new Barrier(participantCount: 2);

        Task<(ProcessarEventoResultado? Resultado, Exception? Excecao)> Run() => Task.Run(async () =>
        {
            startBarrier.SignalAndWait();
            using var scope = fixture.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<ProcessarWebhookStripeHandler>();
            try
            {
                var r = await handler.ProcessarEventoAsync(evento);
                return ((ProcessarEventoResultado?)r, (Exception?)null);
            }
            catch (Exception ex)
            {
                return ((ProcessarEventoResultado?)null, (Exception?)ex);
            }
        });

        var resultados = await Task.WhenAll(Run(), Run());

        resultados.Where(r => r.Excecao is not null).Should().BeEmpty(
            "nenhuma entrega concorrente deve estourar 500: {0}",
            string.Join(" || ", resultados.Where(r => r.Excecao is not null).Select(r => r.Excecao!.GetType().Name + ": " + r.Excecao!.Message)));

        resultados.Count(r => r.Resultado == ProcessarEventoResultado.Aplicado).Should().Be(1,
            "exatamente uma entrega aplica o efeito");
        resultados.Count(r => r.Resultado == ProcessarEventoResultado.JaConsistente).Should().Be(1,
            "a entrega perdedora (xmin ou guard de status) é idempotente");

        using var queryScope = fixture.Services.CreateScope();
        var dbFinal = queryScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pago = await dbFinal.Pagamentos.FirstAsync(p => p.AssinaturaAlunoId == assinaturaId && p.StripePaymentIntentId == paymentIntentId);
        pago.Status.Should().Be(PagamentoStatus.Pago, "efeito único: pagamento termina Pago");
    }

    private async Task<Guid> CriarAssinaturaTreinadorAtivaAsync()
    {
        var (treinadorId, _) = await TreinadorAprovadoComPlanoAsync();
        var planoId = await ObterPlanoFreeIdAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var assinatura = AssinaturaTreinador.Criar(treinadorId, planoId, 50m, now).Value;
        assinatura.Ativar(now);
        db.AssinaturasTreinador.Add(assinatura);
        await db.SaveChangesAsync();
        return assinatura.Id;
    }

    private record HandlerOutcome(bool Success, Guid? PagamentoId, Exception? Exception);

    private static string FormatFalha(HandlerOutcome r) =>
        r.Exception?.GetType().Name + ": " + (r.Exception?.Message ?? "<sem exception>");

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
