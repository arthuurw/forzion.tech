using FluentAssertions;
using forzion.tech.Application.UseCases.Treinadores.ProcessarLimiteAlunos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.E2E;

// Race real (Testcontainers) do job de graça de limite de alunos (T10). Duas execuções
// PARALELAS do handler sobre o MESMO treinador simulam tanto "apara x apara" quanto,
// pelo mesmo mecanismo de proteção (xmin do Treinador + índice único de dedup da
// Notificacao), "apara x regularização concorrente" — em ambos os casos, o segundo
// commit a alcançar o SaveChanges perde a corrida (DbUpdateConcurrencyException) e é
// descartado pelo handler, que segue sem lançar. Docker indisponível neste ambiente:
// compila aqui, execução real fica para o CI.
[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class ProcessarLimiteAlunosRaceTests(RealPipelineFixture fixture)
{
    private const string SenhaHash = "hash-e2e-teste";

    [Fact]
    public async Task ProcessarLimiteAlunos_DuasExecucoesParalelas_ApuraApenasUmaVezSemDuplicarNotificacao()
    {
        var (treinadorId, contaTreinadorId) = await SeedTreinadorAcimaDoCapComDeadlineVencidoAsync(vinculosAtivos: 11);

        using var startBarrier = new Barrier(participantCount: 2);

        Task<Exception?> Run() => Task.Run(async () =>
        {
            startBarrier.SignalAndWait();
            using var scope = fixture.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<ProcessarLimiteAlunosHandler>();
            try
            {
                await handler.HandleAsync();
                return (Exception?)null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        });

        var excecoes = await Task.WhenAll(Run(), Run());

        excecoes.Should().OnlyContain(e => e == null,
            "conflito de concorrência otimista (xmin) é capturado e descartado internamente, nunca propaga: {0}",
            string.Join(" || ", excecoes.Where(e => e is not null).Select(e => e!.GetType().Name + ": " + e.Message)));

        using var queryScope = fixture.Services.CreateScope();
        var db = queryScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ativosRestantes = await db.VinculosTreinadorAluno
            .CountAsync(v => v.TreinadorId == treinadorId && v.Status == VinculoStatus.Ativo);
        ativosRestantes.Should().Be(10, "plano Free comporta 10 — exatamente 1 excedente deve ter sido aparado, uma única vez");

        var treinadorFinal = await db.Treinadores.AsNoTracking().FirstAsync(t => t.Id == treinadorId);
        treinadorFinal.AlunosAcimaDoCapDesde.Should().BeNull("apara bem-sucedida regulariza o carimbo de graça");

        var notificacoesAplicado = await db.Notificacoes
            .CountAsync(n => n.DestinatarioContaId == contaTreinadorId && n.Tipo == TipoNotificacao.LimiteAlunosAplicado);
        notificacoesAplicado.Should().Be(1, "índice único de dedup por dia impede notificação duplicada mesmo sob corrida real");
    }

    private async Task<(Guid TreinadorId, Guid ContaTreinadorId)> SeedTreinadorAcimaDoCapComDeadlineVencidoAsync(int vinculosAtivos)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agora = DateTime.UtcNow;

        var freePlanoId = await db.PlanosPlataforma
            .Where(p => p.Tier == TierPlano.Free && p.IsAtivo)
            .Select(p => p.Id)
            .FirstAsync();

        var emailTreinador = Email.Criar($"race-t{Guid.NewGuid():N}@e2e.test").Value;
        var contaTreinador = Conta.Criar(emailTreinador, SenhaHash, TipoConta.Treinador, agora).Value;
        var treinador = Treinador.Criar(contaTreinador.Id, "Treinador Race", agora, planoPlataformaId: freePlanoId).Value;
        treinador.Aprovar(Guid.NewGuid(), agora);
        treinador.MarcarAcimaDoCap(agora.AddMonths(-4));
        await db.Contas.AddAsync(contaTreinador);
        await db.Treinadores.AddAsync(treinador);
        await db.SaveChangesAsync();

        var pacote = Pacote.Criar(treinador.Id, "Pacote Race", 50m, agora).Value;
        await db.Pacotes.AddAsync(pacote);
        await db.SaveChangesAsync();

        for (var i = 0; i < vinculosAtivos; i++)
        {
            var emailAluno = Email.Criar($"race-a{Guid.NewGuid():N}@e2e.test").Value;
            var contaAluno = Conta.Criar(emailAluno, SenhaHash, TipoConta.Aluno, agora).Value;
            var aluno = Aluno.Criar(contaAluno.Id, $"Aluno Race {i}", agora).Value;
            await db.Contas.AddAsync(contaAluno);
            await db.Alunos.AddAsync(aluno);
            await db.SaveChangesAsync();

            var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, agora.AddDays(-i)).Value;
            vinculo.Aprovar(treinador.Id, pacote.Id, agora.AddDays(-i));
            await db.VinculosTreinadorAluno.AddAsync(vinculo);
        }
        await db.SaveChangesAsync();

        return (treinador.Id, contaTreinador.Id);
    }
}
