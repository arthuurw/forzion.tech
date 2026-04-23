using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IGrupoMuscularRepository
{
    Task<GrupoMuscular?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GrupoMuscular?> ObterPorNomeAsync(string nome, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GrupoMuscular>> ListarTodosAsync(CancellationToken cancellationToken = default);
    Task AdicionarAsync(GrupoMuscular grupoMuscular, CancellationToken cancellationToken = default);
    void Excluir(GrupoMuscular grupoMuscular);
}
