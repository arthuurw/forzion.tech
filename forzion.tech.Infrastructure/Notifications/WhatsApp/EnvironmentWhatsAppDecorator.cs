using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

// Decora o IWhatsAppNotifier real. Em não-produção (WhatsAppSettings.MarcarComoTeste)
// aplica o guardrail de destinatário (redirect/allowlist por telefone). Em produção é
// passthrough puro. Análogo ao EnvironmentEmailDecorator.
public sealed class EnvironmentWhatsAppDecorator(
    IWhatsAppNotifier inner,
    WhatsAppSettings settings,
    ILogger<EnvironmentWhatsAppDecorator> logger) : IWhatsAppNotifier
{
    public bool Habilitado => inner.Habilitado;

    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
        => inner.SendAsync(ResolverDestinatario(phoneNumber), message, cancellationToken);

    public Task SendTemplateAsync(string phoneNumber, WhatsAppTemplateMessage message, CancellationToken cancellationToken = default)
        => inner.SendTemplateAsync(ResolverDestinatario(phoneNumber), message, cancellationToken);

    private string ResolverDestinatario(string phone)
    {
        if (!settings.MarcarComoTeste)
            return phone;

        if (string.IsNullOrWhiteSpace(settings.RedirecionarDestinatariosPara) || TelefonePermitido(phone))
            return phone;

        var alvos = settings.RedirecionarDestinatariosPara
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (alvos.Length == 0)
            return phone;

        logger.LogInformation(
            "WhatsApp de teste redirecionado: destinatário original {Original} -> {Redirect}", phone, alvos[0]);
        return alvos[0];
    }

    private bool TelefonePermitido(string phone)
    {
        if (string.IsNullOrWhiteSpace(settings.AllowlistTelefones))
            return false;

        var soDigitos = Digitos.Apenas(phone);
        return settings.AllowlistTelefones
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(t => Digitos.Apenas(t) == soDigitos);
    }
}
