using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class ContaRegistradaEmailHandler(
    EmailVerificationSender sender) : IDomainEventHandler<ContaRegistradaEvent>
{
    public Task HandleAsync(ContaRegistradaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return sender.EnviarAsync(domainEvent.ContaId, domainEvent.Email, cancellationToken);
    }
}
