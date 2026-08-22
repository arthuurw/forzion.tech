using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class LeadRepository(AppDbContext context) : ILeadRepository
{
    public async Task AdicionarAsync(Lead lead, CancellationToken cancellationToken = default) =>
        await context.Leads.AddAsync(lead, cancellationToken).ConfigureAwait(false);

    public async Task<Lead?> ObterPorIdempotencyKeyAsync(Guid treinadorId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        await context.Leads
            .FirstOrDefaultAsync(l => l.TreinadorId == treinadorId && l.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

    public async Task<Lead?> ObterComHistoricoAsync(Guid treinadorId, Guid leadId, CancellationToken cancellationToken = default) =>
        await context.Leads
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.TreinadorId == treinadorId && l.Id == leadId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<LeadListItem> Items, int Total)> ListarAsync(
        Guid treinadorId,
        int pagina,
        int tamanhoPagina,
        LeadStatus? status = null,
        LeadSource? origem = null,
        DateTime? inicio = null,
        DateTime? fim = null,
        string? termo = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Leads.AsNoTracking().Where(l => l.TreinadorId == treinadorId);

        if (status.HasValue)
            query = query.Where(l => l.Status == status.Value);

        if (origem.HasValue)
            query = query.Where(l => l.Source == origem.Value);

        if (inicio.HasValue)
            query = query.Where(l => l.CreatedAt >= inicio.Value);

        if (fim.HasValue)
            query = query.Where(l => l.CreatedAt <= fim.Value);

        if (!string.IsNullOrWhiteSpace(termo))
            query = query.Where(l => EF.Functions.ILike(l.Nome, $"%{termo}%") || EF.Functions.ILike(l.Contato.Valor, $"%{termo}%"));

        query = query.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(l => new LeadListItem(l.Id, l.Nome, l.Contato.Tipo, l.Contato.Valor, l.Source, l.Status, l.CreatedAt, l.Anonimizado))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task<LeadMetricas> AgregarMetricasAsync(Guid treinadorId, DateTime inicio, DateTime fim, CancellationToken cancellationToken = default)
    {
        var baseQuery = context.Leads
            .AsNoTracking()
            .Where(l => l.TreinadorId == treinadorId && l.CreatedAt >= inicio && l.CreatedAt <= fim);

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var porAgente = await baseQuery.CountAsync(l => l.Source == LeadSource.Agent, cancellationToken).ConfigureAwait(false);
        var porManual = await baseQuery.CountAsync(l => l.Source == LeadSource.Manual, cancellationToken).ConfigureAwait(false);
        var convertidos = await baseQuery.CountAsync(l => l.Status == LeadStatus.Convertido, cancellationToken).ConfigureAwait(false);

        var porMotivo = await baseQuery
            .Where(l => l.MotivoDescarte != null)
            .GroupBy(l => l.MotivoDescarte!.Value)
            .Select(g => new { Motivo = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.Motivo, x => x.Quantidade, cancellationToken)
            .ConfigureAwait(false);

        return new LeadMetricas(total, porAgente, porManual, convertidos, porMotivo);
    }

    public async Task<int> AnonimizarInativosAsync(DateTime cutoff, DateTime agora, CancellationToken cancellationToken = default)
    {
        var leads = await context.Leads
            .Where(l => !l.Anonimizado && l.UltimoToqueEm < cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var lead in leads)
            lead.Anonimizar(agora);

        if (leads.Count > 0)
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return leads.Count;
    }

    public async Task<int> AnonimizarPorTreinadorAsync(Guid treinadorId, DateTime agora, CancellationToken cancellationToken = default)
    {
        var leads = await context.Leads
            .Where(l => l.TreinadorId == treinadorId && !l.Anonimizado)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var lead in leads)
            lead.Anonimizar(agora);

        return leads.Count;
    }

    public async Task<IReadOnlyList<Lead>> BuscarPorContatoCrossTenantAsync(string valorNormalizado, CancellationToken cancellationToken = default) =>
        await context.Leads
            .AsNoTracking()
            .Where(l => l.Contato.Valor == valorNormalizado)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
