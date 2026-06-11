namespace forzion.tech.Infrastructure.Outbox;

// Handler de efeito externo do tipo fx:* (ex.: chamada a API externa). O dispatcher
// roteia pela propriedade Tipo. Exceção propaga → worker faz retry.
public interface IOutboxEfeitoHandler
{
    string Tipo { get; }
    Task ExecutarAsync(string payload, CancellationToken cancellationToken = default);
}
