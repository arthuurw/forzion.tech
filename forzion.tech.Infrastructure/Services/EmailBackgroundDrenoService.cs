using Microsoft.Extensions.Hosting;

namespace forzion.tech.Infrastructure.Services;

public sealed class EmailBackgroundDrenoService(
    EmailBackgroundDispatcher dispatcher,
    IHostApplicationLifetime lifetime) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        dispatcher.RegistrarDrenoNoShutdown(lifetime);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
