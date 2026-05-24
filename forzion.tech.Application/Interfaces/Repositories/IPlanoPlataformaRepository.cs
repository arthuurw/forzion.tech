using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IPlanoPlataformaRepository
{
    Task<PlanoPlataforma?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlanoPlataforma>> ListarAsync(CancellationToken cancellationToken = default);
    Task AdicionarAsync(PlanoPlataforma plano, CancellationToken cancellationToken = default);
}
