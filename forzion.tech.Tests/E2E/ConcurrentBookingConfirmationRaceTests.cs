using FluentAssertions;
using forzion.tech.Application.UseCases.Treinadores.Agendamentos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Tests.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.E2E;

[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class ConcurrentBookingConfirmationRaceTests(RealPipelineFixture fixture)
{
    [Fact]
    public async Task Confirmar_CapacidadeUm_DuasConfirmacoesSimultaneasDoMesmoSlot_ExatamenteUmaConfirmaAUmaFalhaComConflito()
    {
        var (treinadorId, pacoteId) = await SeedTenantAsync(capacidadeMaxima: 1);
        var inicioUtc = DateTime.UtcNow.AddDays(3);
        var fimUtc = inicioUtc.AddMinutes(60);
        var solicitacaoAId = await SeedSolicitacaoPendenteAsync(treinadorId, pacoteId, inicioUtc, fimUtc);
        var solicitacaoBId = await SeedSolicitacaoPendenteAsync(treinadorId, pacoteId, inicioUtc, fimUtc);

        var resultados = await ConfirmarEmParaleloAsync(treinadorId, solicitacaoAId, solicitacaoBId);

        resultados.Where(r => r.Excecao is not null).Should().BeEmpty(
            "nenhuma confirmação concorrente deve terminar em erro não tratado (500): {0}",
            string.Join(" || ", resultados.Where(r => r.Excecao is not null).Select(r => r.Excecao!.GetType().Name + ": " + r.Excecao!.Message)));

        resultados.Count(r => r.Sucesso).Should().Be(1, "capacidade 1 admite exatamente uma confirmação");
        resultados.Count(r => !r.Sucesso).Should().Be(1, "a segunda deve falhar com conflito de capacidade, não com erro");

        var confirmadasNoBanco = await ContarConfirmadasAsync(pacoteId);
        confirmadasNoBanco.Should().Be(1, "o estado final do banco — não só os retornos — deve refletir exatamente 1 confirmada");
    }

    [Fact]
    public async Task Confirmar_CapacidadeDois_TresConfirmacoesSimultaneasDoMesmoSlot_ExatamenteDuasConfirmam()
    {
        var (treinadorId, pacoteId) = await SeedTenantAsync(capacidadeMaxima: 2);
        var inicioUtc = DateTime.UtcNow.AddDays(3);
        var fimUtc = inicioUtc.AddMinutes(60);
        var solicitacaoAId = await SeedSolicitacaoPendenteAsync(treinadorId, pacoteId, inicioUtc, fimUtc);
        var solicitacaoBId = await SeedSolicitacaoPendenteAsync(treinadorId, pacoteId, inicioUtc, fimUtc);
        var solicitacaoCId = await SeedSolicitacaoPendenteAsync(treinadorId, pacoteId, inicioUtc, fimUtc);

        var resultados = await ConfirmarEmParaleloAsync(treinadorId, solicitacaoAId, solicitacaoBId, solicitacaoCId);

        resultados.Where(r => r.Excecao is not null).Should().BeEmpty(
            "nenhuma confirmação concorrente deve terminar em erro não tratado (500): {0}",
            string.Join(" || ", resultados.Where(r => r.Excecao is not null).Select(r => r.Excecao!.GetType().Name + ": " + r.Excecao!.Message)));

        resultados.Count(r => r.Sucesso).Should().Be(2, "capacidade 2 admite exatamente duas confirmações");
        resultados.Count(r => !r.Sucesso).Should().Be(1, "a terceira deve falhar com conflito de capacidade");

        var confirmadasNoBanco = await ContarConfirmadasAsync(pacoteId);
        confirmadasNoBanco.Should().Be(2, "o estado final do banco deve refletir exatamente 2 confirmadas");
    }

    [Fact]
    public async Task Confirmar_MesmoHorarioEmPacotesDiferentesDoMesmoTreinador_ExatamenteUmaConfirmaAOutraRecebeConflito()
    {
        // AD-021: a agenda do treinador é o recurso escasso — capacidade 1 em cada pacote não
        // significa 2 vagas simultâneas no mesmo horário quando os pacotes são do mesmo treinador.
        var treinadorId = await SeedTreinadorAsync();
        var pacoteAId = await SeedPacoteAsync(treinadorId, capacidadeMaxima: 1);
        var pacoteBId = await SeedPacoteAsync(treinadorId, capacidadeMaxima: 1);
        var inicioUtc = DateTime.UtcNow.AddDays(3);
        var fimUtc = inicioUtc.AddMinutes(60);
        var solicitacaoNoPacoteA = await SeedSolicitacaoPendenteAsync(treinadorId, pacoteAId, inicioUtc, fimUtc);
        var solicitacaoNoPacoteB = await SeedSolicitacaoPendenteAsync(treinadorId, pacoteBId, inicioUtc, fimUtc);

        var resultados = await ConfirmarEmParaleloAsync(treinadorId, solicitacaoNoPacoteA, solicitacaoNoPacoteB);

        resultados.Where(r => r.Excecao is not null).Should().BeEmpty(
            "nenhuma confirmação concorrente cross-pacote deve terminar em erro não tratado (500): {0}",
            string.Join(" || ", resultados.Where(r => r.Excecao is not null).Select(r => r.Excecao!.GetType().Name + ": " + r.Excecao!.Message)));

        resultados.Count(r => r.Sucesso).Should().Be(1, "a agenda do treinador só comporta uma confirmação nesse horário, mesmo em pacotes diferentes");

        var confirmadasDoTreinador = await ContarConfirmadasPorTreinadorAsync(treinadorId, inicioUtc, fimUtc);
        confirmadasDoTreinador.Should().Be(1, "o estado final do banco deve refletir exatamente 1 confirmada na agenda do treinador");
    }

    private async Task<HandlerOutcome[]> ConfirmarEmParaleloAsync(Guid treinadorId, params Guid[] solicitacaoIds)
    {
        using var startBarrier = new Barrier(participantCount: solicitacaoIds.Length);

        Task<HandlerOutcome> Run(Guid solicitacaoId) => Task.Run(async () =>
        {
            startBarrier.SignalAndWait();
            using var scope = fixture.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<ConfirmarSolicitacaoHandler>();
            try
            {
                var result = await handler.HandleAsync(treinadorId, solicitacaoId);
                return new HandlerOutcome(result.IsSuccess, null);
            }
            catch (Exception ex)
            {
                return new HandlerOutcome(false, ex);
            }
        });

        return await Task.WhenAll(solicitacaoIds.Select(Run));
    }

    private async Task<int> ContarConfirmadasAsync(Guid pacoteId)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SolicitacoesAgendamento
            .Where(s => s.PacoteId == pacoteId && s.Status == SolicitacaoAgendamentoStatus.Confirmada)
            .CountAsync();
    }

    private async Task<(Guid TreinadorId, Guid PacoteId)> SeedTenantAsync(int capacidadeMaxima)
    {
        var treinadorId = await SeedTreinadorAsync();
        var pacoteId = await SeedPacoteAsync(treinadorId, capacidadeMaxima);
        return (treinadorId, pacoteId);
    }

    private async Task<Guid> SeedTreinadorAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agora = DateTime.UtcNow;

        var email = Email.Criar($"t{Guid.NewGuid():N}@e2e.test").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, agora).Value;
        var treinador = Treinador.Criar(conta.Id, "Treinador Race", agora).Value;
        db.Contas.Add(conta);
        db.Treinadores.Add(treinador);

        await db.SaveChangesAsync();
        return treinador.Id;
    }

    private async Task<Guid> SeedPacoteAsync(Guid treinadorId, int capacidadeMaxima)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agora = DateTime.UtcNow;

        var pacote = new PacoteBuilder().ComTreinadorId(treinadorId).Em(agora).Build();
        pacote.AtualizarCatalogoPublico("Categoria", 60, false, agora, capacidadeMaxima: capacidadeMaxima);
        db.Pacotes.Add(pacote);

        await db.SaveChangesAsync();
        return pacote.Id;
    }

    private async Task<int> ContarConfirmadasPorTreinadorAsync(Guid treinadorId, DateTime inicioUtc, DateTime fimUtc)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SolicitacoesAgendamento
            .Where(s => s.TreinadorId == treinadorId
                && s.Status == SolicitacaoAgendamentoStatus.Confirmada
                && s.InicioUtc < fimUtc && s.FimUtc > inicioUtc)
            .CountAsync();
    }

    private async Task<Guid> SeedSolicitacaoPendenteAsync(Guid treinadorId, Guid pacoteId, DateTime inicioUtc, DateTime fimUtc)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agora = DateTime.UtcNow;

        var contato = ContatoLead.Criar(TipoContatoLead.Email, $"lead{Guid.NewGuid():N}@e2e.test").Value;
        var consentimento = ConsentimentoLead.Criar("Contato comercial", agora, agora).Value;
        var lead = Lead.Criar(treinadorId, "Lead Race", contato, null, consentimento, null, LeadSource.Agent, null, null, agora).Value;
        db.Leads.Add(lead);

        var solicitacao = SolicitacaoAgendamento.Criar(
            treinadorId, pacoteId, lead.Id, $"slot-{Guid.NewGuid():N}", inicioUtc, fimUtc,
            $"idem-{Guid.NewGuid():N}", "hash", agora).Value;
        db.SolicitacoesAgendamento.Add(solicitacao);

        await db.SaveChangesAsync();
        return solicitacao.Id;
    }

    private sealed record HandlerOutcome(bool Sucesso, Exception? Excecao);
}
