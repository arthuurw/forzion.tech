using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace forzion.tech.Tests.Infrastructure.Services;

public class DomainEventDispatcherTests
{
    private sealed record EventoFake(DateTime OcorridoEm) : IDomainEvent;

    private sealed class HandlerQueLanca : IDomainEventHandler<EventoFake>
    {
        public Task HandleAsync(EventoFake domainEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("falha simulada no handler");
    }

    private sealed class HandlerQueRegistra : IDomainEventHandler<EventoFake>
    {
        public bool Executado { get; private set; }

        public Task HandleAsync(EventoFake domainEvent, CancellationToken cancellationToken = default)
        {
            Executado = true;
            return Task.CompletedTask;
        }
    }

    private sealed class HandlerQueCancela : IDomainEventHandler<EventoFake>
    {
        private readonly CancellationTokenSource _cts;
        public HandlerQueCancela(CancellationTokenSource cts) => _cts = cts;

        public Task HandleAsync(EventoFake domainEvent, CancellationToken cancellationToken = default)
        {
            _cts.Cancel();
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private static DomainEventDispatcher CriarDispatcher(IServiceProvider sp) =>
        new(sp, NullLogger<DomainEventDispatcher>.Instance);

    [Fact]
    public async Task DispatchAsync_HandlerLanca_LogaESegueParaOProximo()
    {
        var registrador = new HandlerQueRegistra();
        var services = new ServiceCollection();
        services.AddScoped<IDomainEventHandler<EventoFake>, HandlerQueLanca>();
        services.AddScoped<IDomainEventHandler<EventoFake>>(_ => registrador);
        var sp = services.BuildServiceProvider();

        var act = async () =>
            await CriarDispatcher(sp).DispatchAsync([new EventoFake(DateTime.UtcNow)]);

        await act.Should().NotThrowAsync();
        registrador.Executado.Should().BeTrue("a falha do primeiro handler não pode impedir os demais");
    }

    [Fact]
    public async Task DispatchAsync_TokenCancelado_PropagaOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        var services = new ServiceCollection();
        services.AddScoped<IDomainEventHandler<EventoFake>>(_ => new HandlerQueCancela(cts));
        var sp = services.BuildServiceProvider();

        var act = async () =>
            await CriarDispatcher(sp).DispatchAsync([new EventoFake(DateTime.UtcNow)], cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
