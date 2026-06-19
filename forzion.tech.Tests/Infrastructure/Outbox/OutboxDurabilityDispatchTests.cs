using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Services;
using forzion.tech.Tests.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Tests.Infrastructure.Outbox;

// Partição durável vs best-effort no dispatch in-memory (sem DB — rápido).
public class OutboxDurabilityDispatchTests
{
    private sealed record EventoFake(DateTime OcorridoEm) : IDomainEvent;

    private sealed class HandlerDuravel : IDomainEventHandler<EventoFake>
    {
        public bool Executado { get; private set; }
        public Task HandleAsync(EventoFake domainEvent, CancellationToken cancellationToken = default)
        {
            Executado = true;
            return Task.CompletedTask;
        }
    }

    private sealed class HandlerDuravelQueLanca : IDomainEventHandler<EventoFake>
    {
        public Task HandleAsync(EventoFake domainEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("falha durável");
    }

    private sealed class HandlerNotificacao : IDomainEventHandler<EventoFake>
    {
        public bool Executado { get; private set; }
        public Task HandleAsync(EventoFake domainEvent, CancellationToken cancellationToken = default)
        {
            Executado = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DispatchAsync_EventoDuravel_PulaHandlerDuravelMasRodaNotificacao()
    {
        var duravel = new HandlerDuravel();
        var notificacao = new HandlerNotificacao();
        var services = new ServiceCollection();
        services.AddScoped<IDomainEventHandler<EventoFake>>(_ => duravel);
        services.AddScoped<IDomainEventHandler<EventoFake>>(_ => notificacao);
        using var sp = services.BuildServiceProvider();

        var registry = new OutboxDurabilityRegistry()
            .Registrar<EventoFake, HandlerDuravel>(_ => "k");
        var dispatcher = new DispatcherComCapturaBackground(sp, registry);

        await dispatcher.DispatchAsync([new EventoFake(DateTime.UtcNow)]);
        await dispatcher.DrenarAsync();

        duravel.Executado.Should().BeFalse("handler durável roda no worker, não in-memory");
        notificacao.Executado.Should().BeTrue("notificação best-effort do mesmo evento continua in-memory");
    }

    [Fact]
    public async Task DispatchDuravelAsync_RodaSoHandlerDuravelEPropagaExcecao()
    {
        var notificacao = new HandlerNotificacao();
        var services = new ServiceCollection();
        services.AddScoped<IDomainEventHandler<EventoFake>, HandlerDuravelQueLanca>();
        services.AddScoped<IDomainEventHandler<EventoFake>>(_ => notificacao);
        using var sp = services.BuildServiceProvider();

        var registry = new OutboxDurabilityRegistry()
            .Registrar<EventoFake, HandlerDuravelQueLanca>(_ => "k");
        var dispatcher = new DispatcherComCapturaBackground(sp, registry);

        var act = async () => await dispatcher.DispatchDuravelAsync(new EventoFake(DateTime.UtcNow));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("falha durável");
        notificacao.Executado.Should().BeFalse("DispatchDuravel só roda os handlers duráveis (propaga p/ retry)");
    }
}
