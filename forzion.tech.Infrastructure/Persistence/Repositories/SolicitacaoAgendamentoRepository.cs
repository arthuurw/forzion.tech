using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class SolicitacaoAgendamentoRepository(AppDbContext context) : ISolicitacaoAgendamentoRepository
{
    public async Task AdicionarAsync(SolicitacaoAgendamento solicitacao, CancellationToken cancellationToken = default) =>
        await context.SolicitacoesAgendamento.AddAsync(solicitacao, cancellationToken).ConfigureAwait(false);

    // Tracked (não AsNoTracking): caminho de mutação (confirmar/recusar/cancelar) — precedente
    // fatia 2 (LeadRepository.ObterComHistoricoAsync).
    public async Task<SolicitacaoAgendamento?> ObterPorIdAsync(Guid id, Guid treinadorId, CancellationToken cancellationToken = default) =>
        await context.SolicitacoesAgendamento
            .FirstOrDefaultAsync(s => s.Id == id && s.TreinadorId == treinadorId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<SolicitacaoAgendamento?> ObterPorIdempotencyKeyAsync(Guid treinadorId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        await context.SolicitacoesAgendamento
            .FirstOrDefaultAsync(s => s.TreinadorId == treinadorId && s.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> ContarConfirmadasSobrepostasAsync(Guid treinadorId, Guid pacoteId, DateTime inicioUtc, DateTime fimUtc, CancellationToken cancellationToken = default) =>
        await context.SolicitacoesAgendamento
            .AsNoTracking()
            .Where(s => s.TreinadorId == treinadorId
                && s.PacoteId == pacoteId
                && s.Status == SolicitacaoAgendamentoStatus.Confirmada
                && s.InicioUtc < fimUtc && s.FimUtc > inicioUtc)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<SolicitacaoAgendamento>> ListarConfirmadasNoIntervaloAsync(Guid treinadorId, Guid pacoteId, DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        await context.SolicitacoesAgendamento
            .AsNoTracking()
            .Where(s => s.TreinadorId == treinadorId
                && s.PacoteId == pacoteId
                && s.Status == SolicitacaoAgendamentoStatus.Confirmada
                && s.InicioUtc < ateUtc && s.FimUtc > deUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<SolicitacaoAgendamento> Items, int Total)> ListarPorTreinadorAsync(
        Guid treinadorId,
        SolicitacaoAgendamentoStatus? status,
        int pagina,
        int tamanhoPagina,
        CancellationToken cancellationToken = default)
    {
        var query = context.SolicitacoesAgendamento.AsNoTracking().Where(s => s.TreinadorId == treinadorId);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        query = query.OrderBy(s => s.InicioUtc).ThenBy(s => s.Id);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }
}
