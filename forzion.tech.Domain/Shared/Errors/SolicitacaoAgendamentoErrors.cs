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
    public static Error TransicaoNaoSuportada => Error.Validation("solicitacao_agendamento.transicao_nao_suportada", "Esta transição de status não é suportada por esta operação.");
    public static Error SlotJaIniciado => Error.Business("solicitacao_agendamento.slot_ja_iniciado", "Não é possível confirmar uma solicitação cujo horário já começou.");
    public static Error MotivoMuitoLongo => Error.Validation("solicitacao_agendamento.motivo_muito_longo", "O motivo deve ter no máximo 500 caracteres.");
    public static Error NaoEncontrada => Error.NotFound("solicitacao_agendamento.nao_encontrada", "Solicitação de agendamento não encontrada.");
    public static Error CapacidadeEsgotada => Error.Conflict("solicitacao_agendamento.capacidade_esgotada", "A capacidade máxima do horário já foi atingida.");
}
