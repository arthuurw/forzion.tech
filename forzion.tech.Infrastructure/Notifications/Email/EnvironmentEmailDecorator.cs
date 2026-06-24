using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Settings;
using forzion.tech.Infrastructure.Common;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.Email;

// Decora o IEmailService real. Em não-produção (EmailSettings.MarcarComoTeste) marca o
// e-mail como teste (prefixo no assunto + banner) e aplica o guardrail de destinatário
// (redirect/allowlist). Em produção é passthrough puro.
public sealed class EnvironmentEmailDecorator(
    IEmailService inner,
    EmailSettings settings,
    ILogger<EnvironmentEmailDecorator> logger) : IEmailService
{
    private const string Banner =
        """<div style="background:#b91c1c;color:#ffffff;padding:12px 16px;font-family:Arial,sans-serif;font-size:14px;text-align:center;font-weight:bold">E-mail de teste - ambiente não-produtivo. Não é uma comunicação oficial forzion.tech.</div>""";

    public bool Habilitado => inner.Habilitado;

    public Task EnviarAsync(string para, string assunto, string htmlBody, CancellationToken cancellationToken = default, string? replyTo = null)
    {
        if (!settings.MarcarComoTeste)
            return inner.EnviarAsync(para, assunto, htmlBody, cancellationToken, replyTo);

        return inner.EnviarAsync(
            ResolverDestinatario(para),
            AplicarPrefixo(assunto),
            Banner + htmlBody,
            cancellationToken,
            replyTo);
    }

    private string AplicarPrefixo(string assunto) =>
        string.IsNullOrWhiteSpace(settings.PrefixoAssuntoTeste)
            ? assunto
            : $"{settings.PrefixoAssuntoTeste} {assunto}";

    private string ResolverDestinatario(string para)
    {
        if (string.IsNullOrWhiteSpace(settings.RedirecionarDestinatariosPara) || DominioPermitido(para))
            return para;

        var alvos = settings.RedirecionarDestinatariosPara
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (alvos.Length == 0)
            return para;

        logger.LogInformation(
            "E-mail de teste redirecionado: destinatário original {Original} -> {Redirect}", MascaraPii.Email(para), alvos[0]);
        return alvos[0];
    }

    private bool DominioPermitido(string para)
    {
        if (string.IsNullOrWhiteSpace(settings.AllowlistDominios))
            return false;

        var arroba = para.LastIndexOf('@');
        if (arroba < 0)
            return false;

        var dominio = para[(arroba + 1)..];
        return settings.AllowlistDominios
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(d => string.Equals(d, dominio, StringComparison.OrdinalIgnoreCase));
    }
}
