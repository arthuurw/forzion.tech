using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IEmailDeliveryLogRepository
{
    Task AdicionarAsync(EmailDeliveryLog log, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, int>> ContarPorEventoDesdeAsync(DateTime desde, CancellationToken cancellationToken = default);
}
