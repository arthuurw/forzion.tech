using forzion.tech.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Services;

public sealed class EmailBackgroundDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<EmailBackgroundDispatcher> logger) : IEmailBackgroundDispatcher
{
    public void Disparar(Func<IEmailService, CancellationToken, Task> envio)
    {
        ArgumentNullException.ThrowIfNull(envio);
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                await envio(emailService, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha no envio de e-mail em background.");
            }
        });
    }
}
