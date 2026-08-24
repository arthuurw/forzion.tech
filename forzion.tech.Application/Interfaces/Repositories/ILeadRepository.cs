using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ILeadRepository
{
    Task AdicionarAsync(Lead lead, CancellationToken cancellationToken = default);

    Task<Lead?> ObterPorIdempotencyKeyAsync(Guid treinadorId, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<Lead?> ObterComHistoricoAsync(Guid treinadorId, Guid leadId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<LeadListItem> Items, int Total)> ListarAsync(
        Guid treinadorId,
        int pagina,
        int tamanhoPagina,
        LeadStatus? status = null,
        LeadSource? origem = null,
        DateTime? inicio = null,
        DateTime? fim = null,
        string? termo = null,
        CancellationToken cancellationToken = default);

    Task<LeadMetricas> AgregarMetricasAsync(Guid treinadorId, DateTime inicio, DateTime fim, CancellationToken cancellationToken = default);

    Task<int> AnonimizarInativosAsync(DateTime cutoff, DateTime agora, CancellationToken cancellationToken = default);

    Task<int> AnonimizarPorTreinadorAsync(Guid treinadorId, DateTime agora, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Lead>> BuscarPorContatoCrossTenantAsync(string valorNormalizado, CancellationToken cancellationToken = default);

    Task<Lead?> ObterPorIdCrossTenantAsync(Guid leadId, CancellationToken cancellationToken = default);

    // D-I: dedup de lead do agente. Status Novo/EmContato, não anonimizado, mais recente primeiro
    // (R5 — leads não tem unique por contato, então mais de um pode qualificar).
    Task<Lead?> ObterReutilizavelPorContatoAsync(Guid treinadorId, string valorNormalizado, CancellationToken cancellationToken = default);
}
