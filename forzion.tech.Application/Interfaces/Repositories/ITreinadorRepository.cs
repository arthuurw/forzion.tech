using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ITreinadorRepository
{
    Task<Treinador?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Treinador?> ObterPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Treinador treinador, CancellationToken cancellationToken = default);
}
