using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class LeadRepositoryTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTime Agora = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    private static LeadRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<Guid> SeedTreinadorAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"t{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, Agora).Value;
        var treinador = Treinador.Criar(conta.Id, "Treinador", Agora).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        return treinador.Id;
    }

    private static Lead NovoLead(
        Guid treinadorId,
        string nome = "Fulano",
        string email = null!,
        LeadSource source = LeadSource.Agent,
        DateTime? createdAt = null,
        string? idempotencyKey = null,
        string? argumentosHash = null) =>
        Lead.Criar(
            treinadorId,
            nome,
            ContatoLead.Criar(TipoContatoLead.Email, email ?? $"{Guid.NewGuid():N}@lead.com").Value,
            "quero treinar",
            ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value,
            null,
            source,
            idempotencyKey,
            argumentosHash,
            createdAt ?? Agora).Value;

    private static async Task<Guid> SeedAlunoAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"a{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Aluno, Agora).Value;
        var aluno = Aluno.Criar(conta.Id, "Aluno", Agora).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.SaveChangesAsync();
        return aluno.Id;
    }

    private static async Task<Lead> SeedLeadAsync(AppDbContext ctx, Lead lead)
    {
        ctx.Set<Lead>().Add(lead);
        await ctx.SaveChangesAsync();
        return lead;
    }

    [Fact]
    public async Task ObterPorIdempotencyKeyAsync_ChaveExisteNoTreinador_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var lead = await SeedLeadAsync(ctx, NovoLead(treinadorId, idempotencyKey: "chave-x", argumentosHash: "hash-x"));

        var encontrado = await Repo(ctx).ObterPorIdempotencyKeyAsync(treinadorId, "chave-x");

        encontrado.Should().NotBeNull();
        encontrado!.Id.Should().Be(lead.Id);
    }

    [Fact]
    public async Task ObterPorIdempotencyKeyAsync_ChaveDeOutroTreinador_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorA = await SeedTreinadorAsync(ctx);
        var treinadorB = await SeedTreinadorAsync(ctx);
        await SeedLeadAsync(ctx, NovoLead(treinadorA, idempotencyKey: "chave-y"));

        var encontrado = await Repo(ctx).ObterPorIdempotencyKeyAsync(treinadorB, "chave-y");

        encontrado.Should().BeNull();
    }

    [Fact]
    public async Task ObterComHistoricoAsync_EntidadeRetornadaEhTracked_MutacaoPersisteNoCommit()
    {
        // ObterComHistoricoAsync alimenta os handlers de mutação da esteira (status, interação,
        // convite, conversão) — se viesse AsNoTracking, a mutação em memória nunca chegaria ao
        // banco no SaveChanges seguinte (bug real, sem teste até esta task).
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var lead = await SeedLeadAsync(ctx, NovoLead(treinadorId));

        var carregado = await Repo(ctx).ObterComHistoricoAsync(treinadorId, lead.Id);
        carregado!.MarcarEmContato(Guid.NewGuid(), "liguei", Agora.AddHours(1));
        await ctx.SaveChangesAsync();

        await using var verificacao = fixture.CreateContext();
        var recarregado = await Repo(verificacao).ObterComHistoricoAsync(treinadorId, lead.Id);
        recarregado!.Status.Should().Be(LeadStatus.EmContato);
        recarregado.Interacoes.Should().ContainSingle(i => i.Observacao == "liguei");
    }

    [Fact]
    public async Task ObterComHistoricoAsync_CarregaInteracoesEEscopoPorTreinador()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorA = await SeedTreinadorAsync(ctx);
        var treinadorB = await SeedTreinadorAsync(ctx);
        var lead = NovoLead(treinadorA);
        lead.MarcarEmContato(Guid.NewGuid(), "liguei", Agora.AddHours(1));
        await SeedLeadAsync(ctx, lead);

        var doProprio = await Repo(ctx).ObterComHistoricoAsync(treinadorA, lead.Id);
        var deOutro = await Repo(ctx).ObterComHistoricoAsync(treinadorB, lead.Id);

        doProprio.Should().NotBeNull();
        doProprio!.Interacoes.Should().ContainSingle();
        deOutro.Should().BeNull();
    }

    [Fact]
    public async Task ListarAsync_RetornaApenasDoTreinadorOrdenadoPorChegadaRecente()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorA = await SeedTreinadorAsync(ctx);
        var treinadorB = await SeedTreinadorAsync(ctx);
        var maisAntigo = await SeedLeadAsync(ctx, NovoLead(treinadorA, createdAt: Agora));
        var maisRecente = await SeedLeadAsync(ctx, NovoLead(treinadorA, createdAt: Agora.AddHours(2)));
        await SeedLeadAsync(ctx, NovoLead(treinadorB, createdAt: Agora.AddHours(3)));

        var (items, total) = await Repo(ctx).ListarAsync(treinadorA, pagina: 1, tamanhoPagina: 10);

        total.Should().Be(2);
        items.Select(i => i.Id).Should().ContainInOrder(maisRecente.Id, maisAntigo.Id);
    }

    [Fact]
    public async Task ListarAsync_FiltraPorStatusOrigemPeriodoETermoCombinados()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alvo = NovoLead(treinadorId, nome: "Ana Interessada", email: "ana@lead.com", source: LeadSource.Agent, createdAt: Agora.AddDays(1));
        await SeedLeadAsync(ctx, alvo);

        await SeedLeadAsync(ctx, NovoLead(treinadorId, nome: "Bruno", source: LeadSource.Manual, createdAt: Agora.AddDays(1)));

        var emContato = NovoLead(treinadorId, nome: "Carla", source: LeadSource.Agent, createdAt: Agora.AddDays(1));
        emContato.MarcarEmContato(Guid.NewGuid(), null, Agora.AddDays(1).AddHours(1));
        await SeedLeadAsync(ctx, emContato);

        await SeedLeadAsync(ctx, NovoLead(treinadorId, nome: "Ana Fora Do Periodo", source: LeadSource.Agent, createdAt: Agora.AddDays(-10)));

        var (items, total) = await Repo(ctx).ListarAsync(
            treinadorId, pagina: 1, tamanhoPagina: 10,
            status: LeadStatus.Novo, origem: LeadSource.Agent,
            inicio: Agora, fim: Agora.AddDays(2), termo: "ana");

        total.Should().Be(1);
        items.Should().ContainSingle(i => i.Id == alvo.Id);
    }

    [Fact]
    public async Task ListarAsync_Pagina_RespeitaTamanhoPagina()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        for (var i = 0; i < 5; i++)
            await SeedLeadAsync(ctx, NovoLead(treinadorId, createdAt: Agora.AddMinutes(i)));

        var (items, total) = await Repo(ctx).ListarAsync(treinadorId, pagina: 2, tamanhoPagina: 2);

        total.Should().Be(5);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task AgregarMetricasAsync_ContaPorOrigemEConversao()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        await SeedLeadAsync(ctx, NovoLead(treinadorId, source: LeadSource.Agent, createdAt: Agora));
        await SeedLeadAsync(ctx, NovoLead(treinadorId, source: LeadSource.Manual, createdAt: Agora));

        var alunoId = await SeedAlunoAsync(ctx);
        var convertido = NovoLead(treinadorId, source: LeadSource.Agent, createdAt: Agora);
        convertido.Converter(alunoId, Agora.AddHours(1));
        await SeedLeadAsync(ctx, convertido);

        var descartado = NovoLead(treinadorId, source: LeadSource.Agent, createdAt: Agora);
        descartado.Descartar(MotivoDescarteLead.SemInteresse, Guid.NewGuid(), null, Agora.AddHours(1));
        await SeedLeadAsync(ctx, descartado);

        var metricas = await Repo(ctx).AgregarMetricasAsync(treinadorId, Agora.AddDays(-1), Agora.AddDays(1));

        metricas.Total.Should().Be(4);
        metricas.PorOrigemAgente.Should().Be(3);
        metricas.PorOrigemManual.Should().Be(1);
        metricas.Convertidos.Should().Be(1);
        metricas.PorMotivoDescarte.Should().ContainKey(MotivoDescarteLead.SemInteresse).WhoseValue.Should().Be(1);
    }

    [Fact]
    public async Task AgregarMetricasAsync_SemLeadsNoPeriodo_RetornaZeros()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);

        var metricas = await Repo(ctx).AgregarMetricasAsync(treinadorId, Agora.AddDays(-5), Agora.AddDays(-1));

        metricas.Total.Should().Be(0);
        metricas.PorOrigemAgente.Should().Be(0);
        metricas.Convertidos.Should().Be(0);
        metricas.PorMotivoDescarte.Should().BeEmpty();
    }

    [Fact]
    public async Task AnonimizarPorTreinadorAsync_AfetaSoOTreinadorInformado()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorA = await SeedTreinadorAsync(ctx);
        var treinadorB = await SeedTreinadorAsync(ctx);
        var leadA = await SeedLeadAsync(ctx, NovoLead(treinadorA, nome: "Alvo"));
        var leadB = await SeedLeadAsync(ctx, NovoLead(treinadorB, nome: "Preservado"));

        var quantidade = await Repo(ctx).AnonimizarPorTreinadorAsync(treinadorA, Agora.AddDays(1));
        await ctx.SaveChangesAsync();

        quantidade.Should().Be(1);
        (await ctx.Set<Lead>().FindAsync(leadA.Id))!.Anonimizado.Should().BeTrue();
        (await ctx.Set<Lead>().FindAsync(leadB.Id))!.Anonimizado.Should().BeFalse();
    }

    [Fact]
    public async Task AnonimizarInativosAsync_SoAfetaLeadsAntesDoCutoff()
    {
        // Banco isolado: o método varre TODOS os leads (cross-tenant, sem escopo de treinador) —
        // no banco compartilhado da fixture pegaria leads semeados por outros testes deste arquivo.
        var connectionString = await fixture.CriarBancoIsoladoAsync();
        await using var ctx = fixture.CreateContext(connectionString);
        var treinadorId = await SeedTreinadorAsync(ctx);
        var antigo = await SeedLeadAsync(ctx, NovoLead(treinadorId, nome: "Antigo", createdAt: Agora));
        var recente = await SeedLeadAsync(ctx, NovoLead(treinadorId, nome: "Recente", createdAt: Agora.AddDays(200)));

        var cutoff = Agora.AddDays(180);
        var quantidade = await Repo(ctx).AnonimizarInativosAsync(cutoff, Agora.AddDays(181));

        quantidade.Should().Be(1);
        (await ctx.Set<Lead>().FindAsync(antigo.Id))!.Anonimizado.Should().BeTrue();
        (await ctx.Set<Lead>().FindAsync(recente.Id))!.Anonimizado.Should().BeFalse();
    }

    [Fact]
    public async Task BuscarPorContatoCrossTenantAsync_EncontraIndependenteDoTreinador()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorA = await SeedTreinadorAsync(ctx);
        var treinadorB = await SeedTreinadorAsync(ctx);
        var contatoAlvo = $"{Guid.NewGuid():N}@lead.com";
        var leadA = await SeedLeadAsync(ctx, NovoLead(treinadorA, email: contatoAlvo));
        await SeedLeadAsync(ctx, NovoLead(treinadorB, email: $"{Guid.NewGuid():N}@lead.com"));

        var encontrados = await Repo(ctx).BuscarPorContatoCrossTenantAsync(contatoAlvo);

        encontrados.Should().ContainSingle(l => l.Id == leadA.Id);
    }

    [Fact]
    public async Task BuscarPorContatoCrossTenantAsync_SemResultado_RetornaListaVazia()
    {
        await using var ctx = fixture.CreateContext();

        var encontrados = await Repo(ctx).BuscarPorContatoCrossTenantAsync($"{Guid.NewGuid():N}@inexistente.com");

        encontrados.Should().BeEmpty();
    }
}
