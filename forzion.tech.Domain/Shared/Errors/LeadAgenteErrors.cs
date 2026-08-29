namespace forzion.tech.Domain.Shared.Errors;

public static class LeadAgenteErrors
{
    public static Error ConsentimentoNaoConcedido => Error.Validation("lead_agente.consentimento_nao_concedido", "O consentimento não foi concedido.");
    public static Error TipoContatoInvalido => Error.Validation("lead_agente.tipo_contato_invalido", "O tipo de contato informado é inválido.");
    public static Error IdempotencyKeyObrigatoria => Error.Validation("lead_agente.idempotency_key_obrigatoria", "A chave de idempotência é obrigatória.");
    public static Error IdempotencyKeyMuitoLonga => Error.Validation("lead_agente.idempotency_key_muito_longa", "A chave de idempotência deve ter no máximo 200 caracteres.");
    public static Error IdempotencyConflito => Error.Conflict("lead_agente.idempotency_conflito", "A chave de idempotência já foi usada com argumentos diferentes.");
}
