using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace forzion.tech.Tests.Infrastructure.Services;

internal sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
{
    public CancellationToken ApplicationStarted => CancellationToken.None;
    public CancellationToken ApplicationStopping => CancellationToken.None;
    public CancellationToken ApplicationStopped => CancellationToken.None;
    public void StopApplication() { }
}

// E2E sem worker assíncrono: roda o trabalho best-effort inline (síncrono) p/ que o efeito
// seja observável logo após a request e não corra contra o descarte do Testcontainer.
internal sealed class DispatcherBestEffortSincrono(
    IServiceProvider serviceProvider,
    IServiceScopeFactory scopeFactory,
    OutboxDurabilityRegistry registry,
    ILogger<DomainEventDispatcher> logger)
    : DomainEventDispatcher(serviceProvider, scopeFactory, registry, logger)
{
    protected override void ExecutarEmBackground(Func<CancellationToken, Task> trabalho) =>
        trabalho(CancellationToken.None).GetAwaiter().GetResult();
}

// Intercepta o agendamento background do dispatcher: o trabalho best-effort fica retido até
// DrenarAsync, tornando determinística a asserção "DispatchAsync não roda o handler inline".
internal sealed class DispatcherComCapturaBackground(
    IServiceProvider serviceProvider,
    OutboxDurabilityRegistry registry)
    : DomainEventDispatcher(
        serviceProvider,
        serviceProvider.GetRequiredService<IServiceScopeFactory>(),
        registry,
        NullLogger<DomainEventDispatcher>.Instance,
        new FakeHostApplicationLifetime())
{
    private readonly List<Func<CancellationToken, Task>> _agendados = [];

    public int Agendados => _agendados.Count;

    protected override void ExecutarEmBackground(Func<CancellationToken, Task> trabalho) =>
        _agendados.Add(trabalho);

    public async Task DrenarAsync()
    {
        foreach (var trabalho in _agendados)
            await trabalho(CancellationToken.None);
        _agendados.Clear();
    }
}
