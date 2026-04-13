using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ITenantRepository
{
    Task<Tenant?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SlugExisteAsync(string slug, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Tenant tenant, CancellationToken cancellationToken = default);
}
