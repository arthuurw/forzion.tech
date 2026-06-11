namespace forzion.tech.Domain.Shared.Errors;

public static class OutboxErrors
{
    public static Error TipoObrigatorio => new("outbox.tipo_obrigatorio", "O tipo do efeito é obrigatório.");
    public static Error PayloadObrigatorio => new("outbox.payload_obrigatorio", "O payload do efeito é obrigatório.");
    public static Error ChaveIdempotenciaObrigatoria => new("outbox.chave_idempotencia_obrigatoria", "A chave de idempotência é obrigatória.");
}
