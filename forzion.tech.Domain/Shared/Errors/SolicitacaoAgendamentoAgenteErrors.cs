namespace forzion.tech.Domain.Shared.Errors;

public static class SolicitacaoAgendamentoAgenteErrors
{
    public static Error ConsentimentoNaoConcedido => Error.Validation("solicitacao_agendamento_agente.consentimento_nao_concedido", "O consentimento não foi concedido.");
    public static Error TipoContatoInvalido => Error.Validation("solicitacao_agendamento_agente.tipo_contato_invalido", "O tipo de contato informado é inválido.");
    public static Error SlotNaoEncontrado => Error.NotFound("solicitacao_agendamento_agente.slot_nao_encontrado", "O horário solicitado não está mais disponível.");
    public static Error SlotIndisponivel => Error.Conflict("solicitacao_agendamento_agente.slot_indisponivel", "O horário solicitado já atingiu a capacidade máxima.");
    public static Error IdempotencyConflito => Error.Conflict("solicitacao_agendamento_agente.idempotency_conflito", "A chave de idempotência já foi usada com argumentos diferentes.");
}
