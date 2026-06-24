using System.Net.Http.Json;
using forzion.tech.Application.Interfaces;
using forzion.tech.Infrastructure.Common;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

public class MetaWhatsAppCloudNotifier(
    HttpClient httpClient,
    ILogger<MetaWhatsAppCloudNotifier> logger) : IWhatsAppNotifier
{
    public bool Habilitado => true;

    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var phone = PhoneNumberNormalizer.Normalizar(phoneNumber);
        if (phone is null)
        {
            logger.LogWarning("WhatsApp: telefone inválido/ausente — mensagem de texto ignorada.");
            return Task.CompletedTask;
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = phone,
            type = "text",
            text = new { preview_url = false, body = message }
        };

        return PostAsync(payload, phone, cancellationToken);
    }

    public Task SendTemplateAsync(string phoneNumber, WhatsAppTemplateMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var phone = PhoneNumberNormalizer.Normalizar(phoneNumber);
        if (phone is null)
        {
            logger.LogWarning("WhatsApp: telefone inválido/ausente — template {Template} ignorado.", message.Name);
            return Task.CompletedTask;
        }

        object[] components = message.BodyParameters.Count == 0
            ? []
            :
            [
                new
                {
                    type = "body",
                    parameters = message.BodyParameters
                        .Select(p => new { type = "text", text = p })
                        .ToArray()
                }
            ];

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = phone,
            type = "template",
            template = new
            {
                name = message.Name,
                language = new { code = message.LanguageCode },
                components
            }
        };

        return PostAsync(payload, phone, cancellationToken);
    }

    private async Task PostAsync(object payload, string phone, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient
                .PostAsJsonAsync("messages", payload, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var corpo = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                logger.LogWarning("WhatsApp Cloud API returned {Status}: {Erro}", response.StatusCode, MascaraPii.Scrub(corpo));
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to send WhatsApp message to {Phone}.", MascaraPii.Telefone(phone));
        }
    }
}
