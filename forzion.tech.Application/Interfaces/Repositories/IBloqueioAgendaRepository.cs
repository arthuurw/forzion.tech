using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IBloqueioAgendaRepository
{
    Task<IReadOnlyList<BloqueioAgenda>> ListarVigentesAsync(Guid treinadorId, DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BloqueioAgenda>> ListarPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default);

    Task<BloqueioAgenda?> ObterPorIdAsync(Guid id, Guid treinadorId, CancellationToken cancellationToken = default);

    Task AdicionarAsync(BloqueioAgenda bloqueio, CancellationToken cancellationToken = default);

    void Remover(BloqueioAgenda bloqueio);
}
