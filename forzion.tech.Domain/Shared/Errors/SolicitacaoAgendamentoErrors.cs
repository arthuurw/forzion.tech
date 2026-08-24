namespace forzion.tech.Domain.Shared.Errors;

public static class SolicitacaoAgendamentoErrors
{
    public static Error TreinadorIdInvalido => Error.Validation("solicitacao_agendamento.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error PacoteIdInvalido => Error.Validation("solicitacao_agendamento.pacote_id_invalido", "O identificador do pacote é inválido.");
    public static Error LeadIdInvalido => Error.Validation("solicitacao_agendamento.lead_id_invalido", "O identificador do lead é inválido.");
    public static Error SlotIdObrigatorio => Error.Validation("solicitacao_agendamento.slot_id_obrigatorio", "O identificador do slot é obrigatório.");
    public static Error IntervaloInvalido => Error.Validation("solicitacao_agendamento.intervalo_invalido", "O início do slot deve ser anterior ao fim.");
    public static Error IdempotencyKeyObrigatoria => Error.Validation("solicitacao_agendamento.idempotency_key_obrigatoria", "A chave de idempotência é obrigatória.");
    public static Error IdempotencyKeyMuitoLonga => Error.Validation("solicitacao_agendamento.idempotency_key_muito_longa", "A chave de idempotência deve ter no máximo 200 caracteres.");
}
