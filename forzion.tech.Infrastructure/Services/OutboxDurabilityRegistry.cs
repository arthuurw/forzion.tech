using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;

namespace forzion.tech.Infrastructure.Services;

// Declara quais pares (evento × handler) são DURÁVEIS: rodam no worker do outbox com
// retry, não no dispatch best-effort in-memory. Granularidade por handler (não por evento)
// porque um evento pode ter 1 handler de mutação durável + N notificações best-effort
// (ex.: VinculoAprovadoEvent). Singleton; populado uma vez no DI.
public sealed class OutboxDurabilityRegistry
{
    private readonly Dictionary<Type, Registro> _porEvento = [];
    private readonly Dictionary<string, Type> _porFullName = [];

    // 1ª chamada por evento: fixa a chave de idempotência. O outbox grava UMA linha por evento
    // (uma chave), então um 2º keyed Registrar para o mesmo evento descartaria sua chave em
    // silêncio — falha no boot em vez disso. Handlers duráveis extra usam RegistrarHandlerAdicional.
    public OutboxDurabilityRegistry Registrar<TEvent, THandler>(Func<TEvent, string> chaveIdempotencia)
        where TEvent : IDomainEvent
        where THandler : IDomainEventHandler<TEvent>
    {
        if (_porEvento.ContainsKey(typeof(TEvent)))
            throw new InvalidOperationException(
                $"{typeof(TEvent).Name} já tem chave durável; use RegistrarHandlerAdicional para handlers extra.");

        var registro = new Registro(evento => chaveIdempotencia((TEvent)evento));
        _porEvento[typeof(TEvent)] = registro;
        if (typeof(TEvent).FullName is { } fullName)
            _porFullName[fullName] = typeof(TEvent);

        registro.Handlers.Add(typeof(THandler));
        return this;
    }

    // Handler durável adicional para um evento já registrado: roda na mesma transação do worker
    // e compartilha a chave do evento (não tem chave própria). Exige o evento já registrado.
    public OutboxDurabilityRegistry RegistrarHandlerAdicional<TEvent, THandler>()
        where TEvent : IDomainEvent
        where THandler : IDomainEventHandler<TEvent>
    {
        if (!_porEvento.TryGetValue(typeof(TEvent), out var registro))
            throw new InvalidOperationException(
                $"{typeof(TEvent).Name} não tem handler durável base; registre-o com Registrar primeiro.");

        registro.Handlers.Add(typeof(THandler));
        return this;
    }

    public bool EhDuravel(Type tipoEvento) => _porEvento.ContainsKey(tipoEvento);

    public bool EhHandlerDuravel(Type tipoEvento, Type tipoHandler) =>
        _porEvento.TryGetValue(tipoEvento, out var registro) && registro.Handlers.Contains(tipoHandler);

    public string ChaveIdempotencia(IDomainEvent evento) =>
        _porEvento.TryGetValue(evento.GetType(), out var registro)
            ? registro.ChaveIdempotencia(evento)
            : throw new InvalidOperationException($"Evento {evento.GetType().Name} não é durável.");

    // Resolve o tipo CLR a partir do FullName embutido no `tipo` da linha outbox (evt:<FullName>).
    // Lookup O(1) por FullName (sem varredura linear das chaves a cada linha processada).
    // Restrito aos eventos registrados — entrada desconhecida não vira desserialização arbitrária.
    public Type? ResolverTipoEvento(string fullName) =>
        _porFullName.GetValueOrDefault(fullName);

    private sealed class Registro(Func<IDomainEvent, string> chaveIdempotencia)
    {
        public Func<IDomainEvent, string> ChaveIdempotencia { get; } = chaveIdempotencia;
        public HashSet<Type> Handlers { get; } = [];
    }
}
