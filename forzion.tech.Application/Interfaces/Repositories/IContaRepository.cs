using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IContaRepository
{
    Task<Conta?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conta?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Conta conta, CancellationToken cancellationToken = default);
}
