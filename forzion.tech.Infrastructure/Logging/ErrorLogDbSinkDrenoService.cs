using Microsoft.Extensions.Hosting;

namespace forzion.tech.Infrastructure.Logging;

// Liga o dreno de shutdown do ErrorLogDbSinkProvider ao IHostApplicationLifetime.
// WHY hosted service (e não ctor do provider): o provider é um ILoggerProvider; injetar
// IHostApplicationLifetime nele fecha um ciclo de DI no build do host. Hosted services são
// resolvidos depois do host construído, então aqui o lifetime já é seguro de obter.
public sealed class ErrorLogDbSinkDrenoService(
    ErrorLogDbSinkProvider provider,
    IHostApplicationLifetime lifetime) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        provider.RegistrarDrenoNoShutdown(lifetime);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
