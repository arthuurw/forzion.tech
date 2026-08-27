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

    public async Task<int> ContarConfirmadasSobrepostasAsync(Guid treinadorId, DateTime inicioUtc, DateTime fimUtc, CancellationToken cancellationToken = default) =>
        await context.SolicitacoesAgendamento
            .AsNoTracking()
            .Where(s => s.TreinadorId == treinadorId
                && s.Status == SolicitacaoAgendamentoStatus.Confirmada
                && s.InicioUtc < fimUtc && s.FimUtc > inicioUtc)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<SolicitacaoAgendamento>> ListarConfirmadasNoIntervaloAsync(Guid treinadorId, DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        await context.SolicitacoesAgendamento
            .AsNoTracking()
            .Where(s => s.TreinadorId == treinadorId
                && s.Status == SolicitacaoAgendamentoStatus.Confirmada
                && s.InicioUtc < ateUtc && s.FimUtc > deUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    // Projeta serviço (Pacote) e lead na mesma consulta (sem N+1) — a esteira do treinador
    // precisa dos dois pra exibir a lista sem round-trip extra por item.
    public async Task<(IReadOnlyList<SolicitacaoAgendamentoListItem> Items, int Total)> ListarPorTreinadorAsync(
        Guid treinadorId,
        SolicitacaoAgendamentoStatus? status,
        int pagina,
        int tamanhoPagina,
        CancellationToken cancellationToken = default)
    {
        var query = context.SolicitacoesAgendamento.AsNoTracking().Where(s => s.TreinadorId == treinadorId);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        // Total sobre a query BASE, sem os JOINs de pacote/lead — a contagem não precisa deles,
        // e arrastá-los custaria dois hash joins a mais só para descartar o resultado.
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var projetada =
            from s in query
            join p in context.Pacotes.AsNoTracking() on s.PacoteId equals p.Id
            join l in context.Leads.AsNoTracking() on s.LeadId equals l.Id
            orderby s.InicioUtc, s.Id
            select new SolicitacaoAgendamentoListItem(
                s.Id, s.PacoteId, p.Nome, s.InicioUtc, s.FimUtc, s.Status, s.Motivo, s.CreatedAt,
                l.Id, l.Nome, l.Contato.Tipo, l.Contato.Valor, l.Anonimizado);

        var items = await projetada
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }
}
