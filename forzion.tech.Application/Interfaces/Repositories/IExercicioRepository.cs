using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IExercicioRepository
{
    Task<Exercicio?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Exercicio> Items, int Total)> ListarAsync(Guid tenantId, int pagina, int tamanhoPagina, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Exercicio exercicio, CancellationToken cancellationToken = default);
    Task<bool> ExisteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
