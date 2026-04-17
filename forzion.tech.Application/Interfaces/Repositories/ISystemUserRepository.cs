using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ISystemUserRepository
{
    Task<SystemUser?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SystemUser?> ObterPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(SystemUser systemUser, CancellationToken cancellationToken = default);
}
