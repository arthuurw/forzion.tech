using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using forzion.tech.Tests.Builders;
using Microsoft.EntityFrameworkCore;

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

        var contagem = await Repo(ctx).ContarConfirmadasSobrepostasAsync(treinadorA, pacoteA, inicio, fim);

        contagem.Should().Be(0);
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

        var contagem = await Repo(ctx).ContarConfirmadasSobrepostasAsync(treinadorId, pacoteId, inicioSlot, fimSlot);

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

        var contagem = await Repo(ctx).ContarConfirmadasSobrepostasAsync(treinadorId, pacoteId, inicio, fim);

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

        var resultado = await Repo(ctx).ListarConfirmadasNoIntervaloAsync(treinadorA, pacoteA, Agora, Agora.AddDays(30));

        resultado.Should().BeEmpty();
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
