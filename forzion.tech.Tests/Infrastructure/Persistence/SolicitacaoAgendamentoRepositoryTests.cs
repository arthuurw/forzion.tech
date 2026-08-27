using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using forzion.tech.Tests.Builders;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace forzion.tech.Tests.Infrastructure.Persistence;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class SolicitacaoAgendamentoRepositoryTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTime Agora = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    private static SolicitacaoAgendamentoRepository Repo(AppDbContext ctx) => new(ctx);

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

    private static async Task<Guid> SeedPacoteAsync(AppDbContext ctx, Guid treinadorId)
    {
        var pacote = new PacoteBuilder().ComTreinadorId(treinadorId).Em(Agora).Build();
        await ctx.Pacotes.AddAsync(pacote);
        await ctx.SaveChangesAsync();
        return pacote.Id;
    }

    private static async Task<Guid> SeedLeadAsync(AppDbContext ctx, Guid treinadorId)
    {
        var contato = ContatoLead.Criar(TipoContatoLead.Email, $"lead{Guid.NewGuid():N}@test.com").Value;
        var consentimento = ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value;
        var lead = Lead.Criar(treinadorId, "Lead Teste", contato, null, consentimento, null, LeadSource.Agent, null, null, Agora).Value;
        await ctx.Leads.AddAsync(lead);
        await ctx.SaveChangesAsync();
        return lead.Id;
    }

    private static SolicitacaoAgendamento CriarSolicitacao(
        Guid treinadorId, Guid pacoteId, Guid leadId, string idempotencyKey,
        DateTime? inicioUtc = null, DateTime? fimUtc = null, string? slotId = null, string argumentosHash = "hash") =>
        SolicitacaoAgendamento.Criar(
            treinadorId, pacoteId, leadId,
            slotId ?? $"slot-{Guid.NewGuid():N}",
            inicioUtc ?? Agora.AddDays(1),
            fimUtc ?? Agora.AddDays(1).AddMinutes(30),
            idempotencyKey, argumentosHash, Agora).Value;

    private async Task<(Guid TreinadorId, Guid PacoteId, Guid LeadId)> SeedTenantAsync(AppDbContext ctx)
    {
        var treinadorId = await SeedTreinadorAsync(ctx);
        var pacoteId = await SeedPacoteAsync(ctx, treinadorId);
        var leadId = await SeedLeadAsync(ctx, treinadorId);
        return (treinadorId, pacoteId, leadId);
    }

    [Fact]
    public async Task AdicionarAsync_ReleEmContextoDistinto_PreservaStatusEDados()
    {
        await using var ctxEscrita = fixture.CreateContext();
        var (treinadorId, pacoteId, leadId) = await SeedTenantAsync(ctxEscrita);
        var solicitacao = CriarSolicitacao(treinadorId, pacoteId, leadId, "chave-1");

        await Repo(ctxEscrita).AdicionarAsync(solicitacao);
        await ctxEscrita.SaveChangesAsync();

        await using var ctxLeitura = fixture.CreateContext();
        var lida = await Repo(ctxLeitura).ObterPorIdAsync(solicitacao.Id, treinadorId);

        lida.Should().NotBeNull();
        lida!.Status.Should().Be(SolicitacaoAgendamentoStatus.PendenteAgente);
        lida.TreinadorId.Should().Be(treinadorId);
        lida.PacoteId.Should().Be(pacoteId);
        lida.LeadId.Should().Be(leadId);
        lida.SlotId.Should().Be(solicitacao.SlotId);
        lida.IdempotencyKey.Should().Be("chave-1");
    }

    [Fact]
    public async Task ObterPorIdAsync_SolicitacaoDeOutroTreinador_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var (treinadorA, pacoteA, leadA) = await SeedTenantAsync(ctx);
        var (treinadorB, pacoteB, leadB) = await SeedTenantAsync(ctx);
        var deB = CriarSolicitacao(treinadorB, pacoteB, leadB, "chave-b");
        await Repo(ctx).AdicionarAsync(deB);
        await ctx.SaveChangesAsync();

        var resultado = await Repo(ctx).ObterPorIdAsync(deB.Id, treinadorA);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task ObterPorIdempotencyKeyAsync_SolicitacaoDeOutroTreinador_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var (treinadorA, _, _) = await SeedTenantAsync(ctx);
        var (treinadorB, pacoteB, leadB) = await SeedTenantAsync(ctx);
        await Repo(ctx).AdicionarAsync(CriarSolicitacao(treinadorB, pacoteB, leadB, "chave-compartilhada"));
        await ctx.SaveChangesAsync();

        var resultado = await Repo(ctx).ObterPorIdempotencyKeyAsync(treinadorA, "chave-compartilhada");

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_SolicitacaoDeOutroTreinador_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var (treinadorA, pacoteA, leadA) = await SeedTenantAsync(ctx);
        var (treinadorB, pacoteB, leadB) = await SeedTenantAsync(ctx);
        var deA = CriarSolicitacao(treinadorA, pacoteA, leadA, "chave-a");
        await Repo(ctx).AdicionarAsync(deA);
        await Repo(ctx).AdicionarAsync(CriarSolicitacao(treinadorB, pacoteB, leadB, "chave-b"));
        await ctx.SaveChangesAsync();

        var (items, total) = await Repo(ctx).ListarPorTreinadorAsync(treinadorA, null, 1, 10);

        total.Should().Be(1);
        items.Should().ContainSingle(s => s.Id == deA.Id);
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_SemFiltroDeStatus_PlanoDeExecucaoUsaIndiceDedicado()
    {
        // AUD-42: reproduz a forma real da consulta paginada (WHERE treinador_id + ORDER BY +
        // LIMIT, como em ListarPorTreinadorAsync). Sem o LIMIT o planner prefere um bitmap scan
        // pelo índice (treinador_id, status, inicio_utc) já existente seguido de sort completo —
        // só o LIMIT torna vantajoso evitar esse sort via o índice novo pré-ordenado.
        await using var ctx = fixture.CreateContext();
        var treinadorAlvo = await SeedTreinadorAsync(ctx);
        var pacoteAlvo = await SeedPacoteAsync(ctx, treinadorAlvo);
        var leadAlvo = await SeedLeadAsync(ctx, treinadorAlvo);
        for (var i = 0; i < 200; i++)
            await Repo(ctx).AdicionarAsync(CriarSolicitacao(treinadorAlvo, pacoteAlvo, leadAlvo, $"chave-alvo-{i}", Agora.AddMinutes(i), Agora.AddMinutes(i).AddMinutes(30)));

        for (var t = 0; t < 60; t++)
        {
            var outroTreinador = await SeedTreinadorAsync(ctx);
            var outroPacote = await SeedPacoteAsync(ctx, outroTreinador);
            var outroLead = await SeedLeadAsync(ctx, outroTreinador);
            for (var i = 0; i < 10; i++)
                await Repo(ctx).AdicionarAsync(CriarSolicitacao(outroTreinador, outroPacote, outroLead, $"chave-ruido-{t}-{i}", Agora.AddDays(i), Agora.AddDays(i).AddMinutes(30)));
        }

        await ctx.SaveChangesAsync();

        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using (var analyze = conn.CreateCommand())
        {
            analyze.CommandText = "ANALYZE solicitacoes_agendamento";
            await analyze.ExecuteNonQueryAsync();
        }

        await using var explain = conn.CreateCommand();
        explain.CommandText = @"EXPLAIN (FORMAT TEXT)
            SELECT id, inicio_utc FROM solicitacoes_agendamento
            WHERE treinador_id = @treinadorId
            ORDER BY inicio_utc, id
            LIMIT 20";
        explain.Parameters.AddWithValue("treinadorId", treinadorAlvo);

        var linhas = new List<string>();
        await using (var reader = await explain.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                linhas.Add(reader.GetString(0));

        var plano = string.Join('\n', linhas);
        plano.Should().Contain("ix_solicitacoes_agendamento_treinador_id_inicio_utc_id",
            $"o plano deveria usar o índice dedicado à listagem sem filtro de status; plano obtido:\n{plano}");
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_FiltraPorStatus_ExcluiOsQueNaoCorrespondem()
    {
        await using var ctx = fixture.CreateContext();
        var (treinadorId, pacoteId, leadId) = await SeedTenantAsync(ctx);
        var pendente = CriarSolicitacao(treinadorId, pacoteId, leadId, "chave-pendente");
        var confirmada = CriarSolicitacao(treinadorId, pacoteId, leadId, "chave-confirmada");
        confirmada.Confirmar(Guid.NewGuid(), Agora);
        await Repo(ctx).AdicionarAsync(pendente);
        await Repo(ctx).AdicionarAsync(confirmada);
        await ctx.SaveChangesAsync();

        var (items, total) = await Repo(ctx).ListarPorTreinadorAsync(treinadorId, SolicitacaoAgendamentoStatus.Confirmada, 1, 10);

        total.Should().Be(1);
        items.Should().ContainSingle(s => s.Id == confirmada.Id);
        items.Should().NotContain(s => s.Id == pendente.Id);
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_OrdenaPorInicioUtcDescendente()
    {
        // AUD-05: mais recente primeiro — pendente nova não fica escondida atrás de histórico.
        await using var ctx = fixture.CreateContext();
        var (treinadorId, pacoteId, leadId) = await SeedTenantAsync(ctx);
        var maisTarde = CriarSolicitacao(treinadorId, pacoteId, leadId, "chave-mais-tarde", Agora.AddDays(3), Agora.AddDays(3).AddMinutes(30));
        var maisCedo = CriarSolicitacao(treinadorId, pacoteId, leadId, "chave-mais-cedo", Agora.AddDays(1), Agora.AddDays(1).AddMinutes(30));
        var intermediaria = CriarSolicitacao(treinadorId, pacoteId, leadId, "chave-intermediaria", Agora.AddDays(2), Agora.AddDays(2).AddMinutes(30));
        await Repo(ctx).AdicionarAsync(maisTarde);
        await Repo(ctx).AdicionarAsync(maisCedo);
        await Repo(ctx).AdicionarAsync(intermediaria);
        await ctx.SaveChangesAsync();

        var (items, _) = await Repo(ctx).ListarPorTreinadorAsync(treinadorId, null, 1, 10);

        items.Select(i => i.Id).Should().ContainInOrder(maisTarde.Id, intermediaria.Id, maisCedo.Id);
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_MesmoInicioUtc_DesempataPorIdEPaginacaoNaoRepiteNemPerdeItem()
    {
        // AUD-05: sem o desempate por Id, ORDER BY inicio_utc empatado não tem ordem estável —
        // a mesma página pode devolver itens em ordem diferente entre chamadas, fazendo a
        // paginação por offset repetir ou pular linha ao avançar de página.
        await using var ctx = fixture.CreateContext();
        var (treinadorId, pacoteId, leadId) = await SeedTenantAsync(ctx);
        var mesmoInicio = Agora.AddDays(5);
        var solicitacoes = new List<SolicitacaoAgendamento>();
        for (var i = 0; i < 5; i++)
        {
            var s = CriarSolicitacao(treinadorId, pacoteId, leadId, $"chave-empate-{i}", mesmoInicio, mesmoInicio.AddMinutes(30));
            solicitacoes.Add(s);
            await Repo(ctx).AdicionarAsync(s);
        }
        await ctx.SaveChangesAsync();

        var (todosNumaChamada, _) = await Repo(ctx).ListarPorTreinadorAsync(treinadorId, null, 1, 10);
        var esperado = todosNumaChamada.Select(i => i.Id).ToList();

        var pagina1 = await Repo(ctx).ListarPorTreinadorAsync(treinadorId, null, 1, 2);
        var pagina2 = await Repo(ctx).ListarPorTreinadorAsync(treinadorId, null, 2, 2);
        var pagina3 = await Repo(ctx).ListarPorTreinadorAsync(treinadorId, null, 3, 2);

        var idsPaginados = pagina1.Items.Select(i => i.Id)
            .Concat(pagina2.Items.Select(i => i.Id))
            .Concat(pagina3.Items.Select(i => i.Id))
            .ToList();

        idsPaginados.Should().Equal(esperado, "o desempate por Id garante ordem estável entre chamadas de página distintas");
        idsPaginados.Should().BeEquivalentTo(solicitacoes.Select(s => s.Id), "paginação por offset não pode repetir nem perder item");
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_ProjetaNomeDoServicoEDadosDoLeadSemSegundaConsulta()
    {
        await using var ctx = fixture.CreateContext();
        var (treinadorId, pacoteId, leadId) = await SeedTenantAsync(ctx);
        var solicitacao = CriarSolicitacao(treinadorId, pacoteId, leadId, "chave-projecao");
        await Repo(ctx).AdicionarAsync(solicitacao);
        await ctx.SaveChangesAsync();

        var pacote = await ctx.Pacotes.AsNoTracking().SingleAsync(p => p.Id == pacoteId);
        var lead = await ctx.Leads.AsNoTracking().SingleAsync(l => l.Id == leadId);

        var (items, _) = await Repo(ctx).ListarPorTreinadorAsync(treinadorId, null, 1, 10);

        var item = items.Should().ContainSingle().Subject;
        item.PacoteNome.Should().Be(pacote.Nome);
        item.LeadId.Should().Be(leadId);
        item.LeadNome.Should().Be(lead.Nome);
        item.LeadContatoTipo.Should().Be(lead.Contato.Tipo);
        item.LeadContatoValor.Should().Be(lead.Contato.Valor);
        item.LeadAnonimizado.Should().BeFalse();
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_LeadAnonimizado_ItemTrazPlaceholderSemContato()
    {
        await using var ctx = fixture.CreateContext();
        var (treinadorId, pacoteId, leadId) = await SeedTenantAsync(ctx);
        var solicitacao = CriarSolicitacao(treinadorId, pacoteId, leadId, "chave-anonimizado");
        await Repo(ctx).AdicionarAsync(solicitacao);
        await ctx.SaveChangesAsync();

        var lead = await ctx.Leads.SingleAsync(l => l.Id == leadId);
        lead.Anonimizar(Agora);
        await ctx.SaveChangesAsync();

        var (items, _) = await Repo(ctx).ListarPorTreinadorAsync(treinadorId, null, 1, 10);

        var item = items.Should().ContainSingle().Subject;
        item.LeadAnonimizado.Should().BeTrue();
        item.LeadNome.Should().Be("Lead anonimizado");
        item.LeadContatoValor.Should().Be("[anonimizado]");
    }

    [Fact]
    public async Task ContarConfirmadasSobrepostasAsync_SolicitacaoConfirmadaDeOutroTreinador_NaoConta()
    {
        await using var ctx = fixture.CreateContext();
        var (treinadorA, pacoteA, _) = await SeedTenantAsync(ctx);
        var (treinadorB, pacoteB, leadB) = await SeedTenantAsync(ctx);
        var inicio = Agora.AddDays(2);
        var fim = inicio.AddMinutes(30);
        var confirmadaDeB = CriarSolicitacao(treinadorB, pacoteB, leadB, "chave-b", inicio, fim);
        confirmadaDeB.Confirmar(Guid.NewGuid(), Agora);
        await Repo(ctx).AdicionarAsync(confirmadaDeB);
        await ctx.SaveChangesAsync();

        var contagem = await Repo(ctx).ContarConfirmadasSobrepostasAsync(treinadorA, inicio, fim);

        contagem.Should().Be(0);
    }

    [Fact]
    public async Task ContarConfirmadasSobrepostasAsync_ConfirmadaDeOutroPacoteDoMesmoTreinador_Conta()
    {
        // AD-021: a agenda do treinador é o recurso escasso — a contagem não filtra por pacote.
        await using var ctx = fixture.CreateContext();
        var (treinadorId, pacoteA, leadId) = await SeedTenantAsync(ctx);
        var pacoteB = await SeedPacoteAsync(ctx, treinadorId);
        var inicio = Agora.AddDays(2);
        var fim = inicio.AddMinutes(30);
        var confirmadaNoPacoteA = CriarSolicitacao(treinadorId, pacoteA, leadId, "chave-pacote-a", inicio, fim);
        confirmadaNoPacoteA.Confirmar(Guid.NewGuid(), Agora);
        await Repo(ctx).AdicionarAsync(confirmadaNoPacoteA);
        await ctx.SaveChangesAsync();

        var contagem = await Repo(ctx).ContarConfirmadasSobrepostasAsync(treinadorId, inicio, fim);

        contagem.Should().Be(1, "confirmar no pacote A abate a agenda do treinador, que também serve o pacote B");
    }

    [Fact]
    public async Task ContarConfirmadasSobrepostasAsync_SobreposicaoParcial_Conta()
    {
        await using var ctx = fixture.CreateContext();
        var (treinadorId, pacoteId, leadId) = await SeedTenantAsync(ctx);
        var inicioConfirmada = Agora.AddDays(2).AddMinutes(15);
        var fimConfirmada = inicioConfirmada.AddMinutes(30);
        var confirmada = CriarSolicitacao(treinadorId, pacoteId, leadId, "chave-confirmada", inicioConfirmada, fimConfirmada);
        confirmada.Confirmar(Guid.NewGuid(), Agora);
        await Repo(ctx).AdicionarAsync(confirmada);
        await ctx.SaveChangesAsync();

        // Slot consultado começa antes e termina no meio da confirmada — sobreposição parcial, não igualdade.
        var inicioSlot = Agora.AddDays(2);
        var fimSlot = inicioConfirmada.AddMinutes(10);

        var contagem = await Repo(ctx).ContarConfirmadasSobrepostasAsync(treinadorId, inicioSlot, fimSlot);

        contagem.Should().Be(1);
    }

    [Theory]
    [InlineData(nameof(SolicitacaoAgendamentoStatus.PendenteAgente))]
    [InlineData(nameof(SolicitacaoAgendamentoStatus.Recusada))]
    [InlineData(nameof(SolicitacaoAgendamentoStatus.Cancelada))]
    public async Task ContarConfirmadasSobrepostasAsync_IgnoraStatusNaoConfirmada(string statusNome)
    {
        await using var ctx = fixture.CreateContext();
        var (treinadorId, pacoteId, leadId) = await SeedTenantAsync(ctx);
        var inicio = Agora.AddDays(2);
        var fim = inicio.AddMinutes(30);
        var solicitacao = CriarSolicitacao(treinadorId, pacoteId, leadId, $"chave-{statusNome}", inicio, fim);

        switch (statusNome)
        {
            case nameof(SolicitacaoAgendamentoStatus.Recusada):
                solicitacao.Recusar(Guid.NewGuid(), null, Agora);
                break;
            case nameof(SolicitacaoAgendamentoStatus.Cancelada):
                solicitacao.Confirmar(Guid.NewGuid(), Agora);
                solicitacao.Cancelar(Guid.NewGuid(), null, Agora);
                break;
        }

        await Repo(ctx).AdicionarAsync(solicitacao);
        await ctx.SaveChangesAsync();

        var contagem = await Repo(ctx).ContarConfirmadasSobrepostasAsync(treinadorId, inicio, fim);

        contagem.Should().Be(0);
    }

    [Fact]
    public async Task ListarConfirmadasNoIntervaloAsync_SolicitacaoDeOutroTreinador_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var (treinadorA, pacoteA, _) = await SeedTenantAsync(ctx);
        var (treinadorB, pacoteB, leadB) = await SeedTenantAsync(ctx);
        var inicio = Agora.AddDays(2);
        var fim = inicio.AddMinutes(30);
        var confirmadaDeB = CriarSolicitacao(treinadorB, pacoteB, leadB, "chave-b", inicio, fim);
        confirmadaDeB.Confirmar(Guid.NewGuid(), Agora);
        await Repo(ctx).AdicionarAsync(confirmadaDeB);
        await ctx.SaveChangesAsync();

        var resultado = await Repo(ctx).ListarConfirmadasNoIntervaloAsync(treinadorA, Agora, Agora.AddDays(30));

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task ListarConfirmadasNoIntervaloAsync_ConfirmadaDeOutroPacoteDoMesmoTreinador_Retorna()
    {
        // AD-021: mesma agenda serve todos os pacotes do treinador.
        await using var ctx = fixture.CreateContext();
        var (treinadorId, pacoteA, leadId) = await SeedTenantAsync(ctx);
        await SeedPacoteAsync(ctx, treinadorId);
        var inicio = Agora.AddDays(2);
        var fim = inicio.AddMinutes(30);
        var confirmadaNoPacoteA = CriarSolicitacao(treinadorId, pacoteA, leadId, "chave-pacote-a", inicio, fim);
        confirmadaNoPacoteA.Confirmar(Guid.NewGuid(), Agora);
        await Repo(ctx).AdicionarAsync(confirmadaNoPacoteA);
        await ctx.SaveChangesAsync();

        var resultado = await Repo(ctx).ListarConfirmadasNoIntervaloAsync(treinadorId, Agora, Agora.AddDays(30));

        resultado.Should().ContainSingle(s => s.Id == confirmadaNoPacoteA.Id);
    }

    [Fact]
    public async Task SegundaInsercaoComMesmaChaveDeIdempotencia_ViolaUniqueEEhReconhecidaPeloInspector()
    {
        Guid treinadorId, pacoteId, leadId;
        await using (var ctxSeed = fixture.CreateContext())
            (treinadorId, pacoteId, leadId) = await SeedTenantAsync(ctxSeed);

        await using var ctx1 = fixture.CreateContext();
        await Repo(ctx1).AdicionarAsync(CriarSolicitacao(treinadorId, pacoteId, leadId, "chave-duplicada"));
        await ctx1.SaveChangesAsync();

        await using var ctx2 = fixture.CreateContext();
        await Repo(ctx2).AdicionarAsync(CriarSolicitacao(treinadorId, pacoteId, leadId, "chave-duplicada"));
        var act = async () => await ctx2.SaveChangesAsync();

        var excecao = await act.Should().ThrowAsync<DbUpdateException>(
            "o índice único (treinador_id, idempotency_key) deve bloquear a 2ª inserção");
        new NpgsqlDatabaseErrorInspector()
            .EhViolacaoDeUnicidade(excecao.Which)
            .Should().BeTrue("SqlState 23505 é esperado do índice ix_solicitacoes_agendamento_treinador_id_idempotency_key_unique");
    }
}
