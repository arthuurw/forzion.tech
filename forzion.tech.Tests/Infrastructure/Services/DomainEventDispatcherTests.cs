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

    private sealed class HandlerQueObservaGate(BestEffortConcurrencyGate gate) : IDomainEventHandler<EventoFake>
    {
        public int CapacidadeDuranteExecucao { get; private set; } = -1;

        public Task HandleAsync(EventoFake domainEvent, CancellationToken cancellationToken = default)
        {
            CapacidadeDuranteExecucao = gate.CapacidadeDisponivel;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Gate_Saturado_BloqueiaAteRelease()
    {
        using var gate = new BestEffortConcurrencyGate(2);

        await gate.WaitAsync(CancellationToken.None);
        await gate.WaitAsync(CancellationToken.None);
        var terceiro = gate.WaitAsync(CancellationToken.None);

        terceiro.IsCompleted.Should().BeFalse("bound=2 saturado segura a 3ª aquisição");

        gate.Release();
        await terceiro;
        terceiro.IsCompleted.Should().BeTrue("release libera a aquisição pendente");
    }

    [Fact]
    public async Task BestEffort_SeguraSlotDoGateEnquantoRodaHandler()
    {
        using var gate = new BestEffortConcurrencyGate(1);
        var handler = new HandlerQueObservaGate(gate);
        var services = new ServiceCollection();
        services.AddScoped<IDomainEventHandler<EventoFake>>(_ => handler);
        using var sp = services.BuildServiceProvider();
        var dispatcher = new DispatcherComCapturaBackground(sp, new OutboxDurabilityRegistry(), gate);

        await dispatcher.DispatchAsync([new EventoFake(DateTime.UtcNow)]);
        await dispatcher.DrenarAsync();

        handler.CapacidadeDuranteExecucao.Should().Be(0, "o dispatcher segura o único slot enquanto o handler roda");
        gate.CapacidadeDisponivel.Should().Be(1, "o slot é liberado após o trabalho best-effort");
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
