namespace forzion.tech.Domain.Shared.Errors;

public static class OutboxErrors
{
    public static Error TipoObrigatorio => Error.Validation("outbox.tipo_obrigatorio", "O tipo do efeito é obrigatório.");
    public static Error PayloadObrigatorio => Error.Validation("outbox.payload_obrigatorio", "O payload do efeito é obrigatório.");
    public static Error ChaveIdempotenciaObrigatoria => Error.Validation("outbox.chave_idempotencia_obrigatoria", "A chave de idempotência é obrigatória.");
}
