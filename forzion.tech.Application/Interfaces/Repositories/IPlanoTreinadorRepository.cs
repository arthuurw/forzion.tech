using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IPlanoTreinadorRepository
{
    Task<PlanoTreinador?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlanoTreinador>> ListarAsync(CancellationToken cancellationToken = default);
    Task AdicionarAsync(PlanoTreinador plano, CancellationToken cancellationToken = default);
}
