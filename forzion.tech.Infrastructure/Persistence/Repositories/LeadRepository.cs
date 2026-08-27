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

    // Tracked (não AsNoTracking): reusado tanto pela leitura de detalhe quanto pelas mutações da
    // esteira/conversão/admin — mutar o agregado retornado aqui e chamar CommitAsync persiste.
    public async Task<Lead?> ObterComHistoricoAsync(Guid treinadorId, Guid leadId, CancellationToken cancellationToken = default) =>
        await context.Leads
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
        // Uma varredura da janela: os 5 contadores (antes 5 round-trips sequenciais, um por
        // filtro) saem de uma única agregação condicional (Sum de ternário -> CASE WHEN no SQL).
        // GroupBy(l => 1) produz ZERO grupos quando a janela não tem lead nenhum — por isso o
        // FirstOrDefaultAsync pode vir null, tratado abaixo como todos os contadores zerados.
        var agregado = await context.Leads
            .AsNoTracking()
            .Where(l => l.TreinadorId == treinadorId && l.CreatedAt >= inicio && l.CreatedAt <= fim)
            .GroupBy(l => 1)
            .Select(g => new
            {
                Total = g.Count(),
                PorAgente = g.Sum(l => l.Source == LeadSource.Agent ? 1 : 0),
                PorManual = g.Sum(l => l.Source == LeadSource.Manual ? 1 : 0),
                Convertidos = g.Sum(l => l.Status == LeadStatus.Convertido ? 1 : 0),
                SemInteresse = g.Sum(l => l.MotivoDescarte == MotivoDescarteLead.SemInteresse ? 1 : 0),
                ForaDoPerfil = g.Sum(l => l.MotivoDescarte == MotivoDescarteLead.ForaDoPerfil ? 1 : 0),
                SemResposta = g.Sum(l => l.MotivoDescarte == MotivoDescarteLead.SemResposta ? 1 : 0),
                Duplicado = g.Sum(l => l.MotivoDescarte == MotivoDescarteLead.Duplicado ? 1 : 0),
                Outro = g.Sum(l => l.MotivoDescarte == MotivoDescarteLead.Outro ? 1 : 0)
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (agregado is null)
            return new LeadMetricas(0, 0, 0, 0, new Dictionary<MotivoDescarteLead, int>());

        var porMotivo = new Dictionary<MotivoDescarteLead, int>();
        if (agregado.SemInteresse > 0)
            porMotivo[MotivoDescarteLead.SemInteresse] = agregado.SemInteresse;
        if (agregado.ForaDoPerfil > 0)
            porMotivo[MotivoDescarteLead.ForaDoPerfil] = agregado.ForaDoPerfil;
        if (agregado.SemResposta > 0)
            porMotivo[MotivoDescarteLead.SemResposta] = agregado.SemResposta;
        if (agregado.Duplicado > 0)
            porMotivo[MotivoDescarteLead.Duplicado] = agregado.Duplicado;
        if (agregado.Outro > 0)
            porMotivo[MotivoDescarteLead.Outro] = agregado.Outro;

        return new LeadMetricas(agregado.Total, agregado.PorAgente, agregado.PorManual, agregado.Convertidos, porMotivo);
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

    // Tracked de propósito: único outro caminho cross-tenant do repositório, ao lado de
    // BuscarPorContatoCrossTenantAsync — usado pelo admin para anonimizar (precisa mutar).
    public async Task<Lead?> ObterPorIdCrossTenantAsync(Guid leadId, CancellationToken cancellationToken = default) =>
        await context.Leads
            .FirstOrDefaultAsync(l => l.Id == leadId, cancellationToken)
            .ConfigureAwait(false);

    // Tracked (não AsNoTracking): caminho de mutação — o lead retornado recebe RegistrarInteracao.
    public async Task<Lead?> ObterReutilizavelPorContatoAsync(Guid treinadorId, string valorNormalizado, CancellationToken cancellationToken = default) =>
        await context.Leads
            .Where(l => l.TreinadorId == treinadorId
                && l.Contato.Valor == valorNormalizado
                && !l.Anonimizado
                && (l.Status == LeadStatus.Novo || l.Status == LeadStatus.EmContato))
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
}
