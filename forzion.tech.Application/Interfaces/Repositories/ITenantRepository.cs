using forzion.tech.Domain.Entities;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ITenantRepository
{
    Task<Tenant?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SlugExisteAsync(Slug slug, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Tenant tenant, CancellationToken cancellationToken = default);
}
