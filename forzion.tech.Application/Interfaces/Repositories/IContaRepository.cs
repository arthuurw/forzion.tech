using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IContaRepository
{
    Task<Conta?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conta?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default);

    // Projeta só a coluna do epoch (SEC-05), sem materializar a entidade — lido a cada request.
    Task<DateTimeOffset?> ObterEpochSessaoAsync(Guid contaId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Conta conta, CancellationToken cancellationToken = default);
    Task<int> ContarCriadasDesdeAsync(DateTime desde, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> ListarElegivelPurgaLgpdAsync(DateTime threshold, CancellationToken cancellationToken = default);
}
