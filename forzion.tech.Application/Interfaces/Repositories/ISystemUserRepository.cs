using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ISystemUserRepository
{
    Task<SystemUser?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SystemUser?> ObterPorSupabaseIdAsync(Guid supabaseId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(SystemUser systemUser, CancellationToken cancellationToken = default);
}
