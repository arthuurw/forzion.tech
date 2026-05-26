using System.Net.Http.Headers;
using System.Net.Http.Json;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Settings;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Services;

public sealed class ResendEmailService(HttpClient http, string apiKey, string apiUrl, EmailSettings emailSettings, ILogger<ResendEmailService> logger) : IEmailService
{
    private readonly string _apiKey = apiKey;
    private readonly string _apiUrl = apiUrl;
    private readonly string _from = MontarRemetente(emailSettings);

    public bool Habilitado => true;

    private static string MontarRemetente(EmailSettings settings)
    {
        var nome = string.IsNullOrWhiteSpace(settings.FromName) ? "forzion.tech" : settings.FromName;
        var endereco = string.IsNullOrWhiteSpace(settings.FromAddress) ? "noreply@forzion.tech" : settings.FromAddress;
        return $"{nome} <{endereco}>";
    }

    public async Task EnviarAsync(string para, string assunto, string htmlBody, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            from = _from,
            to = new[] { para },
            subject = assunto,
            html = htmlBody
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = JsonContent.Create(payload);

            var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                logger.LogError("Resend retornou {Status}: {Body}", (int)response.StatusCode, body);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Falha ao enviar e-mail para {Para} — assunto: {Assunto}", para, assunto);
        }
    }
}
