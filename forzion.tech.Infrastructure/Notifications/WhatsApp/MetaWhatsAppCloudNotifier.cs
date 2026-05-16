using System.Net.Http.Headers;
using System.Net.Http.Json;
using forzion.tech.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

public class MetaWhatsAppCloudNotifier(
    HttpClient httpClient,
    ILogger<MetaWhatsAppCloudNotifier> logger) : IWhatsAppNotifier
{
    public async Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var phone = phoneNumber
            .Replace("+", "")
            .Replace("-", "")
            .Replace(" ", "")
            .Replace("(", "")
            .Replace(")", "");

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = phone,
            type = "text",
            text = new { preview_url = false, body = message }
        };

        try
        {
            var response = await httpClient
                .PostAsJsonAsync("messages", payload, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                logger.LogWarning("WhatsApp Cloud API returned {Status}: {Body}", response.StatusCode, body);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to send WhatsApp message to {Phone}.", phone);
        }
    }
}
