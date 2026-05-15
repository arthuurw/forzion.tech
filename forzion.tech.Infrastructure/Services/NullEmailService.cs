using forzion.tech.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Services;

public sealed class NullEmailService : IEmailService
{
    public NullEmailService(ILogger<NullEmailService> logger)
    {
        logger.LogWarning("Serviço de e-mail não configurado. E-mails não serão enviados.");
    }

    public bool Habilitado => false;

    public Task EnviarAsync(string para, string assunto, string htmlBody, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
