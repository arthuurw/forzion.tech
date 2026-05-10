using forzion.tech.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications;

public class NullWhatsAppNotifier(ILogger<NullWhatsAppNotifier> logger) : IWhatsAppNotifier
{
    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("WhatsApp notifier not configured. Skipping message to {Phone}.", phoneNumber);
        return Task.CompletedTask;
    }
}
