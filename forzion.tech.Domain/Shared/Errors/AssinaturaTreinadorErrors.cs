namespace forzion.tech.Domain.Shared.Errors;

public static class AssinaturaTreinadorErrors
{
    public static Error TreinadorIdInvalido => Error.Validation("assinatura_treinador.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error PlanoIdInvalido => Error.Validation("assinatura_treinador.plano_id_invalido", "O identificador do plano é inválido.");
    public static Error ValorInvalido => Error.Validation("assinatura_treinador.valor_invalido", "O valor da assinatura deve ser maior que zero.");
    public static Error CanceladaNaoAtivavel => Error.Conflict("assinatura_treinador.cancelada_nao_ativavel", "A assinatura cancelada não pode ser ativada.");
    public static Error ApenasAtivasInadimplentes => Error.Conflict("assinatura_treinador.apenas_ativas_inadimplentes", "Apenas assinaturas ativas podem ser marcadas como inadimplentes.");
    public static Error JaCancelada => Error.Conflict("assinatura_treinador.ja_cancelada", "A assinatura já está cancelada.");
    public static Error ProximaCobrancaNaoFutura => Error.Validation("assinatura_treinador.proxima_cobranca_nao_futura", "A data da próxima cobrança deve ser futura.");
    public static Error InadimplenteDeveUsarRegularizacao => Error.Conflict("assinatura_treinador.inadimplente_deve_usar_regularizacao", "A assinatura inadimplente não pode ser ativada diretamente; registre um pagamento de regularização.");
    public static Error TrocaPlanoEstadoInvalido => Error.Conflict("assinatura_treinador.troca_plano_estado_invalido", "A troca de plano só é permitida em assinatura ativa ou inadimplente.");
    public static Error PlanoAgendadoIdInvalido => Error.Validation("assinatura_treinador.plano_agendado_id_invalido", "O identificador do plano agendado é inválido.");
    public static Error OffboardingNecessario => Error.Conflict("assinatura_treinador.offboarding_necessario", "Não é possível cancelar o plano com alunos ativos ou pendentes vinculados. Desvincule os alunos antes de cancelar.");
    public static Error NaoEncontrada => Error.NotFound("assinatura_treinador.nao_encontrada", "Assinatura não encontrada.");
    public static Error NaoPodeRenovarCancelada => Error.Business("assinatura_treinador.nao_pode_renovar_cancelada", "Assinatura cancelada não pode ser renovada.");
    public static Error NaoPodeRenovarPendente => Error.Business("assinatura_treinador.nao_pode_renovar_pendente", "Assinatura pendente não pode ser renovada.");
}
