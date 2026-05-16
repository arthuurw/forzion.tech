using System.Net.Http.Json;
using forzion.tech.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

public class EvolutionApiWhatsAppNotifier(
    HttpClient httpClient,
    ILogger<EvolutionApiWhatsAppNotifier> logger) : IWhatsAppNotifier
{
    public async Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var phone = phoneNumber.Replace("+", "").Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "");

        var payload = new { number = phone, text = message };

        try
        {
            var response = await httpClient.PostAsJsonAsync("messages/sendText", payload, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                logger.LogWarning("Evolution API returned {Status}: {Body}", response.StatusCode, body);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to send WhatsApp message to {Phone}.", phone);
        }
    }
}
