using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.E2E;

[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class CrossTenantAuthorizationE2ETests(RealPipelineFixture fixture)
{
    private const string SenhaPadrao = "Senha@12345";
    private readonly Dictionary<Guid, string> _emailPorTreinador = new();

    [Fact]
    public async Task TreinadorB_AprovaVinculoPendenteDeA_NegadoEVinculoPermanecePendente()
    {
        var (_, _, vinculoId) = await CenarioTreinadorAComAlunoPendenteAsync();
        var treinadorB = await TreinadorAprovadoComPlanoClienteAsync();

        var resposta = await treinadorB.PostAsJsonAsync(
            $"/treinador/vinculos/{vinculoId}/aprovar", new { pacoteId = Guid.Empty, trarFichas = false });

        resposta.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);

        using var scope = fixture.Services.CreateScope();
        var vinculoRepo = scope.ServiceProvider.GetRequiredService<IVinculoTreinadorAlunoRepository>();
        var vinculo = await vinculoRepo.ObterPorIdAsync(vinculoId);
        vinculo!.Status.Should().Be(VinculoStatus.AguardandoAprovacao);
    }

    [Fact]
    public async Task TreinadorB_LeAlunoDeA_Negado()
    {
        var (_, alunoId, _) = await CenarioTreinadorAComAlunoPendenteAsync();
        var treinadorB = await TreinadorAprovadoComPlanoClienteAsync();

        var resposta = await treinadorB.GetAsync($"/treinador/alunos/{alunoId}");

        resposta.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    private async Task<(Guid treinadorAId, Guid alunoId, Guid vinculoId)> CenarioTreinadorAComAlunoPendenteAsync()
    {
        var treinadorAId = await TreinadorAprovadoComPlanoAsync();
        var treinadorA = ClienteComToken(await LoginTokenAsync(_emailPorTreinador[treinadorAId], SenhaPadrao));
        var pacoteId = await CriarPacoteAsync(treinadorA);
        var alunoId = await RegistrarAlunoAsync(treinadorAId, pacoteId);
        var vinculoId = await ObterVinculoPendenteIdAsync(alunoId);
        return (treinadorAId, alunoId, vinculoId);
    }

    private async Task<HttpClient> TreinadorAprovadoComPlanoClienteAsync()
    {
        var treinadorId = await TreinadorAprovadoComPlanoAsync();
        return ClienteComToken(await LoginTokenAsync(_emailPorTreinador[treinadorId], SenhaPadrao));
    }

    private async Task<Guid> TreinadorAprovadoComPlanoAsync()
    {
        var treinadorId = await RegistrarTreinadorAsync();
        var admin = await ClienteAdminAsync();

        (await admin.PostAsJsonAsync($"/admin/treinadores/{treinadorId}/aprovar", new { }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var freeId = await ObterPlanoFreeIdAsync();
        (await admin.PatchAsJsonAsync($"/admin/treinadores/{treinadorId}/plano", new { planoId = freeId }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        return treinadorId;
    }

    private async Task<Guid> RegistrarTreinadorAsync()
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
        return treinadorId;
    }

    private async Task VerificarEmailDiretoAsync(string email)
    {
        using var scope = fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContaRepository>();
        var conta = await repo.ObterPorEmailAsync(email.Trim().ToLowerInvariant());
        conta!.MarcarEmailVerificado(DateTime.UtcNow);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
    }

    private static async Task<Guid> CriarPacoteAsync(HttpClient treinador)
    {
        var response = await treinador.PostAsJsonAsync(
            "/treinador/pacotes", new { nome = "Pacote E2E", preco = 199.90m, descricao = "Pacote de teste" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("pacoteId").GetGuid();
    }

    private async Task<Guid> RegistrarAlunoAsync(Guid treinadorId, Guid pacoteId)
    {
        var email = $"a{Guid.NewGuid():N}@e2e.test";
        var response = await fixture.CreateClient().PostAsJsonAsync(
            "/auth/register/aluno", new { email, senha = SenhaPadrao, nome = "Aluno E2E", treinadorId, pacoteId });
        var diag = string.Join(" || ", fixture.ErrosCapturados);
        response.StatusCode.Should().Be(HttpStatusCode.Created, "erro servidor: {0}", diag);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("alunoId").GetGuid();
    }

    private async Task<Guid> ObterVinculoPendenteIdAsync(Guid alunoId)
    {
        var admin = await ClienteAdminAsync();
        var vinculo = await admin.GetFromJsonAsync<JsonElement>($"/admin/alunos/{alunoId}/vinculo");
        return vinculo.GetProperty("vinculoPendente").GetProperty("vinculoId").GetGuid();
    }

    private async Task<Guid> ObterPlanoFreeIdAsync()
    {
        var planos = await fixture.CreateClient().GetFromJsonAsync<JsonElement>("/auth/planos");
        return planos.EnumerateArray()
            .First(p => p.GetProperty("nome").GetString() == "Free")
            .GetProperty("planoId").GetGuid();
    }

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
}
