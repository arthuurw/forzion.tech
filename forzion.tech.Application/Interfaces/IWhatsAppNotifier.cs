namespace forzion.tech.Application.Interfaces;

public interface IWhatsAppNotifier
{
    Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}
