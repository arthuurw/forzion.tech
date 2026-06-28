using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IErrorLogRepository
{
    Task AdicionarAsync(ErrorLogEntry entry, CancellationToken cancellationToken = default);
    Task<int> ContarDesdeAsync(DateTime desde, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ErrorLogEntry>> ListarDesdeAsync(DateTime desde, int limite, CancellationToken cancellationToken = default);
    Task<int> LimparAntigosAsync(CancellationToken cancellationToken = default);
}
