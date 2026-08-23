using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.E2E;

// Rotas JWT novas de agenda do treinador (T22): mapeadas, autenticadas, e escopadas por
// treinadorId — cross-tenant colapsa em 404, nunca 403 (AGF3-23), mesmo padrão de isolamento
// já provado pelas fatias 1/2.
[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class AgendaRotasE2ETests(RealPipelineFixture fixture)
{
    private const string SenhaPadrao = "SenhaForte123";

    [Fact]
    public async Task TreinadorA_ApagaBloqueioDeTreinadorB_Retorna404NaoForbidden()
    {
        var (_, clienteA) = await TreinadorAprovadoComClienteAsync();
        var (_, clienteB) = await TreinadorAprovadoComClienteAsync();

        var criar = await clienteB.PostAsJsonAsync("/treinador/agenda/bloqueios", new
        {
            tipo = "Pontual",
            inicioUtc = DateTime.UtcNow.AddDays(1),
            fimUtc = DateTime.UtcNow.AddDays(1).AddHours(1),
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        var corpo = await criar.Content.ReadFromJsonAsync<JsonElement>();
        var bloqueioId = corpo.GetProperty("id").GetGuid();

        var apagar = await clienteA.DeleteAsync($"/treinador/agenda/bloqueios/{bloqueioId}");

        apagar.StatusCode.Should().Be(HttpStatusCode.NotFound);
        apagar.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TreinadorAutenticado_ExerceTodasAsRotasDeAgenda_RespondemAutenticadasComSucesso()
    {
        var (_, cliente) = await TreinadorAprovadoComClienteAsync();

        var listarVazio = await cliente.GetAsync("/treinador/agenda/bloqueios");
        listarVazio.StatusCode.Should().Be(HttpStatusCode.OK);

        var criar = await cliente.PostAsJsonAsync("/treinador/agenda/bloqueios", new
        {
            tipo = "RecorrenteSemanal",
            diaSemana = 1,
            horaInicio = "12:00",
            horaFim = "13:00",
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        var bloqueioId = (await criar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var obterPolitica = await cliente.GetAsync("/treinador/agenda/politica");
        obterPolitica.StatusCode.Should().Be(HttpStatusCode.OK);

        var atualizarPolitica = await cliente.PutAsJsonAsync("/treinador/agenda/politica", new
        {
            antecedenciaMinimaHoras = 4,
            horizonteDias = 30,
        });
        atualizarPolitica.StatusCode.Should().Be(HttpStatusCode.OK);
        var politica = await atualizarPolitica.Content.ReadFromJsonAsync<JsonElement>();
        politica.GetProperty("antecedenciaMinimaHoras").GetInt32().Should().Be(4);
        politica.GetProperty("horizonteDias").GetInt32().Should().Be(30);

        var apagar = await cliente.DeleteAsync($"/treinador/agenda/bloqueios/{bloqueioId}");
        apagar.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // AGF3-16b: a não-persistência de fuso inválido já é provada em nível de handler/domínio
    // (mock Times.Never); este teste prova o mapeamento Result.Validation→400 na rota HTTP real.
    [Fact]
    public async Task AtualizarPerfilPublico_FusoInvalido_Retorna400()
    {
        var (_, cliente) = await TreinadorAprovadoComClienteAsync();

        var salvarPerfil = await cliente.PutAsJsonAsync("/treinador/perfil-publico", new
        {
            nomeFantasia = "Studio Teste",
            endereco = (object?)null,
            politicas = (object?)null,
            horarios = Array.Empty<object>(),
            isPublicado = false,
            fusoHorario = "Nao/Existe_Nesta_Tzdata",
        });

        salvarPerfil.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // AGF3-28: mesmo caso da política — não-persistência já provada em mock; aqui prova-se o
    // status HTTP real de fato retornado pela rota.
    [Fact]
    public async Task AtualizarPoliticaAgenda_HorizonteDiasNaoPositivo_Retorna400()
    {
        var (_, cliente) = await TreinadorAprovadoComClienteAsync();

        var atualizarPolitica = await cliente.PutAsJsonAsync("/treinador/agenda/politica", new
        {
            antecedenciaMinimaHoras = 4,
            horizonteDias = 0,
        });

        atualizarPolitica.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- Helpers de auth/gestão (mesmo padrão duplicado dos outros E2E — sem base compartilhada no repo) ---

    private async Task<(Guid TreinadorId, HttpClient Cliente)> TreinadorAprovadoComClienteAsync()
    {
        var (treinadorId, email) = await RegistrarTreinadorAsync();
        var admin = ClienteComToken(await LoginTokenAsync(RealPipelineFixture.AdminEmail, RealPipelineFixture.AdminPassword));

        using var req = new HttpRequestMessage(HttpMethod.Post, $"/admin/treinadores/{treinadorId}/aprovar")
        {
            Content = JsonContent.Create(new { }),
        };
        req.Headers.Add(RequerStepUpFilter.Header, await fixture.GerarStepUpTokenAsync(RealPipelineFixture.AdminEmail));
        (await admin.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.OK);

        return (treinadorId, ClienteComToken(await LoginTokenAsync(email)));
    }

    private async Task<(Guid TreinadorId, string Email)> RegistrarTreinadorAsync()
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

        using var scope = fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaRepository>();
        var conta = await repo.ObterPorEmailAsync(email.Trim().ToLowerInvariant());
        conta!.MarcarEmailVerificado(DateTime.UtcNow);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();

        return (treinadorId, email);
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
