using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ITreinoRepository
{
    Task<Treino?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Treino> Items, int Total)> ListarAsync(Guid tenantId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Treino> Items, int Total)> ListarPorAlunoAsync(Guid tenantId, Guid alunoId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Treino treino, CancellationToken cancellationToken = default);
}
