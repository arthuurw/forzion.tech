using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Services;

namespace forzion.tech.Tests.Infrastructure.Outbox;

// OUT-01: registro de durabilidade não pode descartar a chave de idempotência em silêncio.
public class OutboxDurabilityRegistryTests
{
    private sealed record EventoFake(DateTime OcorridoEm) : IDomainEvent;
    private sealed class HandlerA : IDomainEventHandler<EventoFake>
    {
        public Task HandleAsync(EventoFake e, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class HandlerB : IDomainEventHandler<EventoFake>
    {
        public Task HandleAsync(EventoFake e, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public void Registrar_SegundoKeyedParaMesmoEvento_Lanca()
    {
        var registry = new OutboxDurabilityRegistry().Registrar<EventoFake, HandlerA>(_ => "a");

        var act = () => registry.Registrar<EventoFake, HandlerB>(_ => "b");

        act.Should().Throw<InvalidOperationException>("a 2ª chave seria descartada em silêncio");
    }

    [Fact]
    public void RegistrarHandlerAdicional_AdicionaHandlerCompartilhandoChave()
    {
        var registry = new OutboxDurabilityRegistry()
            .Registrar<EventoFake, HandlerA>(_ => "k")
            .RegistrarHandlerAdicional<EventoFake, HandlerB>();

        registry.EhHandlerDuravel(typeof(EventoFake), typeof(HandlerA)).Should().BeTrue();
        registry.EhHandlerDuravel(typeof(EventoFake), typeof(HandlerB)).Should().BeTrue();
        registry.ChaveIdempotencia(new EventoFake(DateTime.UtcNow)).Should().Be("k");
    }

    [Fact]
    public void RegistrarHandlerAdicional_SemBase_Lanca()
    {
        var registry = new OutboxDurabilityRegistry();

        var act = () => registry.RegistrarHandlerAdicional<EventoFake, HandlerB>();

        act.Should().Throw<InvalidOperationException>("não há handler durável base para o evento");
    }
}
