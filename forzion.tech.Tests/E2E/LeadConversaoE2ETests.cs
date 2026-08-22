using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Api.Filters;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.RegistrarAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Tests.E2E;

// Cadeia lead -> convite -> resolução pública -> cadastro pelo link -> aluno + vínculo pendente.
// HTTP real + JWT real + Postgres real (Testcontainers); ILeadConviteSender é o único ponto
// substituído (spy no lugar do e-mail) para capturar o token cru, que nunca sai por API.
[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class LeadConversaoE2ETests(RealPipelineFixture fixture)
{
    private const string SenhaPadrao = "SenhaForte123";
    private readonly Dictionary<Guid, string> _emailPorTreinador = new();

    [Fact]
    public async Task Lead_Convite_Cadastro_CriaAlunoComVinculoPendenteDoTreinadorDoConviteEConverteLead()
    {
        var treinadorId = await TreinadorAprovadoComPlanoAsync();
        var treinador = ClienteComToken(await LoginTokenAsync(_emailPorTreinador[treinadorId]));
        var pacoteId = await CriarPacoteAsync(treinador);
        var leadId = await SeedLeadAsync(treinadorId, "lead-convite@e2e.test");

        var emitirConvite = await treinador.PostAsync($"/treinador/leads/{leadId}/convite", null);
        emitirConvite.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenCru = fixture.LeadConviteSenderSpy.UltimoTokenCapturado;
        tokenCru.Should().NotBeNullOrEmpty();

        var resolvido = await fixture.CreateClient().GetAsync($"/auth/convite/{tokenCru}");
        resolvido.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpoResolvido = await resolvido.Content.ReadFromJsonAsync<JsonElement>();
        corpoResolvido.GetProperty("nome").GetString().Should().Be("Fulano Convite");
        corpoResolvido.GetProperty("contatoValor").GetString().Should().Be("lead-convite@e2e.test");
        corpoResolvido.GetProperty("treinadorId").GetGuid().Should().Be(treinadorId);

        // treinadorId do corpo é deliberadamente outro — o servidor deve ignorá-lo (AGF2-41).
        var emailAluno = $"aluno-convite-{Guid.NewGuid():N}@e2e.test";
        var cadastro = await fixture.CreateClient().PostAsJsonAsync("/auth/register/aluno", new
        {
            email = emailAluno,
            senha = SenhaPadrao,
            nome = "Aluno Via Convite",
            treinadorId = Guid.NewGuid(),
            pacoteId,
            conviteToken = tokenCru,
        });
        cadastro.StatusCode.Should().Be(HttpStatusCode.Created, string.Join(" || ", fixture.ErrosCapturados));
        var corpoCadastro = await cadastro.Content.ReadFromJsonAsync<JsonElement>();
        var alunoId = corpoCadastro.GetProperty("alunoId").GetGuid();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vinculo = await db.VinculosTreinadorAluno.SingleAsync(v => v.AlunoId == alunoId);
        vinculo.TreinadorId.Should().Be(treinadorId);
        vinculo.Status.Should().Be(VinculoStatus.AguardandoAprovacao);

        var lead = await db.Leads.SingleAsync(l => l.Id == leadId);
        lead.Status.Should().Be(LeadStatus.Convertido);
        lead.AlunoId.Should().Be(alunoId);

        var convite = await db.LeadConvites.SingleAsync(c => c.LeadId == leadId);
        convite.UsadoEm.Should().NotBeNull();

        // Segunda tentativa com o MESMO token (já consumido) -> cadastro normal, sem 2ª conversão.
        var emailSegundoAluno = $"aluno-segundo-{Guid.NewGuid():N}@e2e.test";
        var segundoCadastro = await fixture.CreateClient().PostAsJsonAsync("/auth/register/aluno", new
        {
            email = emailSegundoAluno,
            senha = SenhaPadrao,
            nome = "Aluno Cadastro Normal",
            treinadorId,
            pacoteId,
            conviteToken = tokenCru,
        });
        segundoCadastro.StatusCode.Should().Be(HttpStatusCode.Created, string.Join(" || ", fixture.ErrosCapturados));
        var corpoSegundo = await segundoCadastro.Content.ReadFromJsonAsync<JsonElement>();
        var segundoAlunoId = corpoSegundo.GetProperty("alunoId").GetGuid();

        var leadAposSegundaTentativa = await db.Leads.SingleAsync(l => l.Id == leadId);
        leadAposSegundaTentativa.AlunoId.Should().Be(alunoId, "a segunda tentativa não deve reconverter o lead");

        var vinculoSegundo = await db.VinculosTreinadorAluno.SingleAsync(v => v.AlunoId == segundoAlunoId);
        vinculoSegundo.TreinadorId.Should().Be(treinadorId, "sem convite válido, usa o treinadorId enviado pelo cliente");
    }

    // Prova de atomicidade contra Postgres real (AGF2-41/43): o pacote é apagado por fora
    // (conexao/transacao separada, ja commitada) depois que o handler ja leu e validou o
    // pacote e ja mutou Convite.Consumir()/Lead.Converter() em memoria — o INSERT do vinculo,
    // na mesma SaveChangesAsync que carrega essas mutacoes, esbarra na FK real e todo o
    // batch reverte junto, nao só o vinculo.
    [Fact]
    public async Task Lead_Convite_Cadastro_FalhaRealDePostgresAposConverterLead_NaoPersisteNadaDaConversao()
    {
        var treinadorId = await TreinadorAprovadoComPlanoAsync();
        var treinador = ClienteComToken(await LoginTokenAsync(_emailPorTreinador[treinadorId]));
        var pacoteId = await CriarPacoteAsync(treinador);
        var leadId = await SeedLeadAsync(treinadorId, "lead-rollback@e2e.test");

        var emitirConvite = await treinador.PostAsync($"/treinador/leads/{leadId}/convite", null);
        emitirConvite.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenCru = fixture.LeadConviteSenderSpy.UltimoTokenCapturado;
        tokenCru.Should().NotBeNullOrEmpty();

        var emailAluno = $"aluno-rollback-{Guid.NewGuid():N}@e2e.test";

        using var scope = fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var pacoteRepoQueApagaAoLer = new PacoteRepositoryApagandoAposLer(
            sp.GetRequiredService<IPacoteRepository>(),
            fixture.Services.GetRequiredService<IServiceScopeFactory>());

        var handler = new RegistrarAlunoHandler(
            sp.GetRequiredService<IContaRepository>(),
            sp.GetRequiredService<IAlunoRepository>(),
            sp.GetRequiredService<IVinculoTreinadorAlunoRepository>(),
            sp.GetRequiredService<ITreinadorRepository>(),
            pacoteRepoQueApagaAoLer,
            sp.GetRequiredService<IPasswordHasher>(),
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<ILogAprovacaoRepository>(),
            sp.GetRequiredService<FluentValidation.IValidator<RegistrarAlunoCommand>>(),
            sp.GetRequiredService<LeadConviteResolver>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<RegistrarAlunoHandler>>());

        var command = new RegistrarAlunoCommand(
            emailAluno, SenhaPadrao, "Aluno Rollback", Guid.NewGuid(), pacoteId, ConviteToken: tokenCru);

        var act = async () => await handler.HandleAsync(command);
        await act.Should().ThrowAsync<DbUpdateException>();

        using var verifyScope = fixture.Services.CreateScope();
        var verifySp = verifyScope.ServiceProvider;
        var verifyDb = verifySp.GetRequiredService<AppDbContext>();

        var leadPersistido = await verifyDb.Leads.AsNoTracking().SingleAsync(l => l.Id == leadId);
        leadPersistido.Status.Should().Be(LeadStatus.Novo, "a conversao inteira deve reverter, nao so o vinculo");
        leadPersistido.AlunoId.Should().BeNull();

        var convitePersistido = await verifyDb.LeadConvites.AsNoTracking().SingleAsync(c => c.LeadId == leadId);
        convitePersistido.UsadoEm.Should().BeNull("o consumo do convite faz parte da mesma transacao revertida");

        var contaCriada = await verifySp.GetRequiredService<IContaRepository>()
            .ObterPorEmailAsync(emailAluno.Trim().ToLowerInvariant());
        contaCriada.Should().BeNull("nenhuma Conta/Aluno deve sobreviver a reversao");
    }

    private sealed class PacoteRepositoryApagandoAposLer(
        IPacoteRepository interno, IServiceScopeFactory scopeFactoryDeOutraConexao) : IPacoteRepository
    {
        public async Task<Pacote?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var pacote = await interno.ObterPorIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (pacote is not null)
            {
                using var scopeApagador = scopeFactoryDeOutraConexao.CreateScope();
                var dbApagador = scopeApagador.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbApagador.Database
                    .ExecuteSqlInterpolatedAsync($"DELETE FROM pacotes WHERE id = {id}", cancellationToken)
                    .ConfigureAwait(false);
            }
            return pacote;
        }

        public Task<IReadOnlyList<Pacote>> ListarPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
            interno.ListarPorTreinadorAsync(treinadorId, cancellationToken);

        public Task AdicionarAsync(Pacote pacote, CancellationToken cancellationToken = default) =>
            interno.AdicionarAsync(pacote, cancellationToken);

        public void Remover(Pacote pacote) => interno.Remover(pacote);

        public Task<bool> ExisteVinculoComPacoteAsync(Guid pacoteId, CancellationToken cancellationToken = default) =>
            interno.ExisteVinculoComPacoteAsync(pacoteId, cancellationToken);

        public Task<IReadOnlyList<Pacote>> ListarAtivosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
            interno.ListarAtivosPorTreinadorAsync(treinadorId, cancellationToken);

        public Task<IReadOnlyList<Pacote>> ListarPublicosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
            interno.ListarPublicosPorTreinadorAsync(treinadorId, cancellationToken);
    }

    // --- Helpers (mesmo padrão duplicado dos outros E2E — sem base compartilhada no repo) ---

    private async Task<Guid> SeedLeadAsync(Guid treinadorId, string emailLead)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var lead = Lead.Criar(
            treinadorId,
            "Fulano Convite",
            ContatoLead.Criar(TipoContatoLead.Email, emailLead).Value,
            "quero treinar",
            ConsentimentoLead.Criar("Contato comercial", DateTime.UtcNow, DateTime.UtcNow).Value,
            null,
            LeadSource.Agent,
            null,
            null,
            DateTime.UtcNow).Value;

        db.Set<Lead>().Add(lead);
        await db.SaveChangesAsync();
        return lead.Id;
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

    private async Task<HttpClient> ClienteAdminAsync() =>
        ClienteComToken(await LoginTokenAsync(RealPipelineFixture.AdminEmail, RealPipelineFixture.AdminPassword));

    private async Task<Guid> ObterPlanoFreeIdAsync()
    {
        var planos = await fixture.CreateClient().GetFromJsonAsync<JsonElement>("/auth/planos");
        return planos.EnumerateArray()
            .First(p => p.GetProperty("nome").GetString() == "Free")
            .GetProperty("planoId").GetGuid();
    }

    private async Task<Guid> RegistrarTreinadorAsync()
    {
        var email = $"t{Guid.NewGuid():N}@e2e.test";
        var planoFreeId = await ObterPlanoFreeIdAsync();
        var response = await fixture.CreateClient().PostAsJsonAsync(
            "/auth/register/treinador",
            new { email, senha = SenhaPadrao, nome = "Treinador Convite E2E", planoPlataformaId = planoFreeId, modoPagamentoAluno = "Plataforma" });
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

    private async Task<Guid> TreinadorAprovadoComPlanoAsync()
    {
        var treinadorId = await RegistrarTreinadorAsync();
        var admin = await ClienteAdminAsync();

        using var req = new HttpRequestMessage(HttpMethod.Post, $"/admin/treinadores/{treinadorId}/aprovar")
        {
            Content = JsonContent.Create(new { }),
        };
        req.Headers.Add(RequerStepUpFilter.Header, await fixture.GerarStepUpTokenAsync(RealPipelineFixture.AdminEmail));
        (await admin.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.OK);

        var freeId = await ObterPlanoFreeIdAsync();
        (await admin.PatchAsJsonAsync($"/admin/treinadores/{treinadorId}/plano", new { planoId = freeId }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        return treinadorId;
    }

    private static async Task<Guid> CriarPacoteAsync(HttpClient treinador)
    {
        var response = await treinador.PostAsJsonAsync(
            "/treinador/pacotes", new { nome = "Pacote Convite E2E", preco = 150m, descricao = "Pacote de teste" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("pacoteId").GetGuid();
    }
}
