using FluentAssertions;
using forzion.tech.Application.UseCases.Engajamento;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class NudgeAderenciaScanIntegrationTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTime Hoje = new(2026, 7, 4, 0, 0, 0, DateTimeKind.Utc);

    private static NudgeAderenciaHandler Handler(AppDbContext ctx) =>
        new(new ExecucaoTreinoRepository(ctx),
            new NotificacaoRepository(ctx, new NpgsqlDatabaseErrorInspector()),
            new FakeTimeProvider(new DateTimeOffset(Hoje.AddHours(12))));

    private static async Task<(Guid contaAlunoId, Guid alunoId, Guid treinoId)> SeedAlunoAtivoAsync(AppDbContext ctx)
    {
        var agora = DateTime.UtcNow;

        var contaTreinador = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Treinador, agora).Value;
        var treinador = Treinador.Criar(contaTreinador.Id, "Carlos", agora).Value;
        var contaAluno = Conta.Criar(Email.Criar($"a{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Aluno, agora).Value;
        var aluno = Aluno.Criar(contaAluno.Id, "João", agora).Value;

        await ctx.Contas.AddRangeAsync(contaTreinador, contaAluno);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.SaveChangesAsync();

        var pacote = Pacote.Criar(treinador.Id, "Pacote", 100m, agora).Value;
        await ctx.Pacotes.AddAsync(pacote);
        await ctx.SaveChangesAsync();

        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, agora).Value;
        vinculo.Aprovar(treinador.Id, pacote.Id, agora);
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);

        var treino = Treino.Criar("Treino A", ObjetivoTreino.Forca, treinador.Id, agora).Value;
        await ctx.Treinos.AddAsync(treino);
        await ctx.SaveChangesAsync();

        return (contaAluno.Id, aluno.Id, treino.Id);
    }

    private static async Task AddExecucaoAsync(AppDbContext ctx, Guid treinoId, Guid alunoId, DateTime data)
    {
        var execucao = ExecucaoTreino.Criar(treinoId, alunoId, data, DateTime.UtcNow).Value;
        execucao.ClearDomainEvents();
        await ctx.ExecucoesTreino.AddAsync(execucao);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Scan_DuasVezesNoMesmoDia_NaoDuplicaNudge()
    {
        await using var ctx = fixture.CreateContext();
        var (contaAlunoId, alunoId, treinoId) = await SeedAlunoAtivoAsync(ctx);
        await AddExecucaoAsync(ctx, treinoId, alunoId, Hoje.AddHours(9));

        await Handler(ctx).HandleAsync();
        await Handler(ctx).HandleAsync();

        await using var verifica = fixture.CreateContext();
        var total = await verifica.Notificacoes
            .CountAsync(n => n.DestinatarioContaId == contaAlunoId && n.Tipo == TipoNotificacao.Reforco);
        total.Should().Be(1);
    }

    [Fact]
    public async Task Scan_DoisConcorrentesNoMesmoDia_GeraExatamenteUmNudge()
    {
        await using var seedCtx = fixture.CreateContext();
        var (contaAlunoId, alunoId, treinoId) = await SeedAlunoAtivoAsync(seedCtx);
        await AddExecucaoAsync(seedCtx, treinoId, alunoId, Hoje.AddHours(9));

        using var barrier = new Barrier(participantCount: 2);

        Task Run() => Task.Run(async () =>
        {
            await using var ctx = fixture.CreateContext();
            barrier.SignalAndWait();
            await Handler(ctx).HandleAsync();
        });

        await Task.WhenAll(Run(), Run());

        await using var verifica = fixture.CreateContext();
        var total = await verifica.Notificacoes
            .CountAsync(n => n.DestinatarioContaId == contaAlunoId && n.Tipo == TipoNotificacao.Reforco);
        total.Should().Be(1);
    }

    [Fact]
    public async Task ProjetarAderenciaAtivos_DiasConsecutivos_CalculaStreak()
    {
        await using var ctx = fixture.CreateContext();
        var (_, alunoId, treinoId) = await SeedAlunoAtivoAsync(ctx);
        for (var i = 0; i < 3; i++)
            await AddExecucaoAsync(ctx, treinoId, alunoId, Hoje.AddDays(-i).AddHours(9));

        var snapshots = await new ExecucaoTreinoRepository(ctx)
            .ProjetarAderenciaAtivosAsync(DateOnly.FromDateTime(Hoje));

        var snapshot = snapshots.Single(s => s.AlunoId == alunoId);
        snapshot.UltimaExecucao.Should().Be(DateOnly.FromDateTime(Hoje));
        snapshot.Streak.Should().Be(3);
    }

    [Fact]
    public async Task Scan_StreakDeSeteDias_GeraReforcoEMarcoStreak()
    {
        await using var ctx = fixture.CreateContext();
        var (contaAlunoId, alunoId, treinoId) = await SeedAlunoAtivoAsync(ctx);
        for (var i = 0; i < 7; i++)
            await AddExecucaoAsync(ctx, treinoId, alunoId, Hoje.AddDays(-i).AddHours(9));

        await Handler(ctx).HandleAsync();

        await using var verifica = fixture.CreateContext();
        var tipos = await verifica.Notificacoes
            .Where(n => n.DestinatarioContaId == contaAlunoId)
            .Select(n => n.Tipo)
            .ToListAsync();
        tipos.Should().Contain(TipoNotificacao.Reforco);
        tipos.Should().Contain(TipoNotificacao.MarcoStreak);
    }
}
