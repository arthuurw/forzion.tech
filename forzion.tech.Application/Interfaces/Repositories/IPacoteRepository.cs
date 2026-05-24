using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IPacoteRepository
{
    Task<Pacote?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Pacote>> ListarPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Pacote pacote, CancellationToken cancellationToken = default);
    void Remover(Pacote pacote);
    Task<bool> ExisteVinculoComPacoteAsync(Guid pacoteId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Pacote>> ListarAtivosPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default);
}
