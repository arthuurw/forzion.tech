using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IContaMfaRepository
{
    Task AdicionarAsync(ContaMfa mfa, CancellationToken cancellationToken = default);
    Task<ContaMfa?> BuscarPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default);
    Task ExcluirPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default);
}
