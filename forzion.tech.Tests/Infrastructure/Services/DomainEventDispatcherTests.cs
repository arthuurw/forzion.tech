using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task DispatchAsync_NaoRodaHandlerInline_SoAposDreno()
    {
        var registrador = new HandlerQueRegistra();
        var services = new ServiceCollection();
        services.AddScoped<IDomainEventHandler<EventoFake>>(_ => registrador);
        using var sp = services.BuildServiceProvider();
        var dispatcher = new DispatcherComCapturaBackground(sp, new OutboxDurabilityRegistry());

        await dispatcher.DispatchAsync([new EventoFake(DateTime.UtcNow)]);

        registrador.Executado.Should().BeFalse("best-effort sai do request-path; não roda inline");
        dispatcher.Agendados.Should().Be(1);

        await dispatcher.DrenarAsync();
        registrador.Executado.Should().BeTrue("o handler roda no escopo de fundo após o dreno");
    }

    [Fact]
    public async Task DispatchAsync_HandlerLanca_IsolaFalhaERodaOProximo()
    {
        var registrador = new HandlerQueRegistra();
        var services = new ServiceCollection();
        services.AddScoped<IDomainEventHandler<EventoFake>, HandlerQueLanca>();
        services.AddScoped<IDomainEventHandler<EventoFake>>(_ => registrador);
        using var sp = services.BuildServiceProvider();
        var dispatcher = new DispatcherComCapturaBackground(sp, new OutboxDurabilityRegistry());

        await dispatcher.DispatchAsync([new EventoFake(DateTime.UtcNow)]);
        var dreno = async () => await dispatcher.DrenarAsync();

        await dreno.Should().NotThrowAsync();
        registrador.Executado.Should().BeTrue("a falha do primeiro handler não pode impedir os demais");
    }
}
