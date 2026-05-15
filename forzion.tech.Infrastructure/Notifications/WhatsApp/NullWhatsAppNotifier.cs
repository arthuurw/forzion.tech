using forzion.tech.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

public class NullWhatsAppNotifier : IWhatsAppNotifier
{
    private readonly ILogger<NullWhatsAppNotifier> _logger;

    public NullWhatsAppNotifier(ILogger<NullWhatsAppNotifier> logger)
    {
        _logger = logger;
        _logger.LogWarning("Serviço de WhatsApp não configurado. Mensagens não serão enviadas.");
    }

    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("WhatsApp notifier not configured. Skipping message to {Phone}.", phoneNumber);
        return Task.CompletedTask;
    }
}
