using System.Text.Json;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class EmailCriticoSolicitadoEmailHandler(
    IEmailService emailService,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<AppSettings> appSettings,
    IHostEnvironment environment,
    ILogger<EmailCriticoSolicitadoEmailHandler> logger) : IDomainEventHandler<EmailCriticoSolicitadoEvent>
{
    public async Task HandleAsync(EmailCriticoSolicitadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var json = dataProtectionProvider.CreateProtector(EmailCriticoDispatcher.ProtectorPurpose)
            .Unprotect(domainEvent.DadosCifrados);
        var dados = JsonSerializer.Deserialize<DadosEmailCritico>(json)
            ?? throw new InvalidOperationException("Payload de e-mail crítico inválido após decifrar.");

        var (assunto, corpo) = Render(domainEvent.Template, dados);

        if (!emailService.Habilitado)
        {
            if (environment.IsDevelopment()
                && domainEvent.Template is EmailCriticoTemplate.CodigoMfa or EmailCriticoTemplate.TrocaEmail)
                logger.LogInformation(
                    "E-mail desabilitado; segredo {Template} para {Destino} = {Segredo}",
                    domainEvent.Template, dados.Destino, dados.Segredo);
            return;
        }

        await emailService.EnviarAsync(dados.Destino, assunto, corpo, cancellationToken).ConfigureAwait(false);
    }

    private (string Assunto, string Corpo) Render(EmailCriticoTemplate template, DadosEmailCritico dados)
    {
        var baseUrl = appSettings.Value.FrontendBaseUrl;
        return template switch
        {
            EmailCriticoTemplate.VerificarEmail => (
                "Confirme seu e-mail — forzion.tech",
                EmailTemplates.VerificarEmail(dados.Destino, $"{baseUrl}/verify-email?token={dados.Segredo}")),
            EmailCriticoTemplate.RedefinirSenha => (
                "Redefinição de senha — forzion.tech",
                EmailTemplates.RedefinirSenha(dados.Destino, $"{baseUrl}/reset-password?token={dados.Segredo}")),
            EmailCriticoTemplate.CodigoMfa => (
                "Seu código de verificação — forzion.tech",
                EmailTemplates.CodigoMfa(dados.Segredo)),
            EmailCriticoTemplate.TrocaEmail => (
                "Confirmação de troca de e-mail — forzion.tech",
                EmailTemplates.TrocaEmailCodigo(dados.Segredo)),
            _ => throw new InvalidOperationException($"Template de e-mail crítico desconhecido: {template}.")
        };
    }
}
