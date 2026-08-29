using FluentAssertions;
using forzion.tech.Application.UseCases.Treinadores.Agendamentos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Tests.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.E2E;

[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class ConcurrentSolicitacaoTransicaoRaceTests(RealPipelineFixture fixture)
{
    [Fact]
    public async Task ConfirmarERecusarEmParaleloNaMesmaSolicitacao_ExatamenteUmaTransicaoVenceAOutraRecebeConflito()
    {
        var (treinadorId, pacoteId) = await SeedTenantAsync(capacidadeMaxima: 5);
        var inicioUtc = DateTime.UtcNow.AddDays(3);
        var fimUtc = inicioUtc.AddMinutes(60);
        var solicitacaoId = await SeedSolicitacaoPendenteAsync(treinadorId, pacoteId, inicioUtc, fimUtc);

        using var startBarrier = new Barrier(participantCount: 2);

        Task<HandlerOutcome> ConfirmarAsync() => Task.Run(async () =>
        {
            startBarrier.SignalAndWait();
            using var scope = fixture.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<ConfirmarSolicitacaoHandler>();
            try
            {
                var result = await handler.HandleAsync(treinadorId, solicitacaoId);
                return new HandlerOutcome(result.IsSuccess, result.IsFailure ? result.Error : null, null);
            }
            catch (Exception ex)
            {
                return new HandlerOutcome(false, null, ex);
            }
        });

        Task<HandlerOutcome> RecusarAsync() => Task.Run(async () =>
        {
            startBarrier.SignalAndWait();
            using var scope = fixture.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<RecusarSolicitacaoHandler>();
            try
            {
                var result = await handler.HandleAsync(treinadorId, solicitacaoId, "Sem vaga");
                return new HandlerOutcome(result.IsSuccess, result.IsFailure ? result.Error : null, null);
            }
            catch (Exception ex)
            {
                return new HandlerOutcome(false, null, ex);
            }
        });

        var resultados = await Task.WhenAll(ConfirmarAsync(), RecusarAsync());

        resultados.Where(r => r.Excecao is not null).Should().BeEmpty(
            "nem confirmar nem recusar concorrentes devem terminar em erro não tratado (500): {0}",
            string.Join(" || ", resultados.Where(r => r.Excecao is not null).Select(r => r.Excecao!.GetType().Name + ": " + r.Excecao!.Message)));

        resultados.Count(r => r.Sucesso).Should().Be(1, "exatamente uma das duas transições concorrentes deve vencer");
        var perdedor = resultados.Single(r => !r.Sucesso);
        perdedor.Erro.Should().Be(SolicitacaoAgendamentoErrors.TransicaoNaoSuportada,
            "a transição perdedora deve ser recusada como conflito de negócio (409), não um erro genérico");

        var statusFinal = await ObterStatusAsync(solicitacaoId);
        statusFinal.Should().BeOneOf(SolicitacaoAgendamentoStatus.Confirmada, SolicitacaoAgendamentoStatus.Recusada);
    }

    private async Task<SolicitacaoAgendamentoStatus> ObterStatusAsync(Guid solicitacaoId)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SolicitacoesAgendamento
            .Where(s => s.Id == solicitacaoId)
            .Select(s => s.Status)
            .SingleAsync();
    }

    private async Task<(Guid TreinadorId, Guid PacoteId)> SeedTenantAsync(int capacidadeMaxima)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agora = DateTime.UtcNow;

        var email = Email.Criar($"t{Guid.NewGuid():N}@e2e.test").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, agora).Value;
        var treinador = Treinador.Criar(conta.Id, "Treinador Race", agora).Value;
        db.Contas.Add(conta);
        db.Treinadores.Add(treinador);

        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).Em(agora).Build();
        pacote.AtualizarCatalogoPublico("Categoria", 60, false, agora, capacidadeMaxima: capacidadeMaxima);
        db.Pacotes.Add(pacote);

        await db.SaveChangesAsync();
        return (treinador.Id, pacote.Id);
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

    private sealed record HandlerOutcome(bool Sucesso, Error? Erro, Exception? Excecao);
}
