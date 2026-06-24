using forzion.tech.Application.Interfaces;
using forzion.tech.Infrastructure.Common;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Services;

public sealed class NullEmailService : IEmailService
{
    private readonly ILogger<NullEmailService> _logger;

    public NullEmailService(ILogger<NullEmailService> logger)
    {
        _logger = logger;
        _logger.LogWarning("Serviço de e-mail não configurado. E-mails não serão enviados.");
    }

    public bool Habilitado => false;

    public Task EnviarAsync(string para, string assunto, string htmlBody, CancellationToken cancellationToken = default, string? replyTo = null)
    {
        _logger.LogDebug("Serviço de e-mail não configurado. E-mail para {Destinatario} ignorado.", MascaraPii.Email(para));
        return Task.CompletedTask;
    }
}
