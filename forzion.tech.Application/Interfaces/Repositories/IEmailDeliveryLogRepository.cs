using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IEmailDeliveryLogRepository
{
    Task AdicionarAsync(EmailDeliveryLog log, CancellationToken cancellationToken = default);
}
