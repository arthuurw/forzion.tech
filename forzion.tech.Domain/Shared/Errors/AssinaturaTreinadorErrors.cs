namespace forzion.tech.Domain.Shared.Errors;

public static class AssinaturaTreinadorErrors
{
    public static Error TreinadorIdInvalido => new("assinatura_treinador.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error PlanoIdInvalido => new("assinatura_treinador.plano_id_invalido", "O identificador do plano é inválido.");
    public static Error ValorInvalido => new("assinatura_treinador.valor_invalido", "O valor da assinatura deve ser maior que zero.");
    public static Error CanceladaNaoAtivavel => new("assinatura_treinador.cancelada_nao_ativavel", "AssinaturaTreinador cancelada não pode ser ativada.");
    public static Error ApenasAtivasInadimplentes => new("assinatura_treinador.apenas_ativas_inadimplentes", "Apenas assinaturas ativas podem ser marcadas como inadimplentes.");
    public static Error JaCancelada => new("assinatura_treinador.ja_cancelada", "A assinatura já está cancelada.");
    public static Error ProximaCobrancaNaoFutura => new("assinatura_treinador.proxima_cobranca_nao_futura", "A data da próxima cobrança deve ser futura.");
    public static Error InadimplenteDeveUsarRegularizacao => Error.Conflict("assinatura_treinador.inadimplente_deve_usar_regularizacao", "Assinatura inadimplente não pode ser ativada diretamente; use RegistrarPagamentoRegularizado.");
    public static Error TrocaPlanoEstadoInvalido => Error.Conflict("assinatura_treinador.troca_plano_estado_invalido", "A troca de plano só é permitida em assinatura ativa ou inadimplente.");
    public static Error PlanoAgendadoIdInvalido => new("assinatura_treinador.plano_agendado_id_invalido", "O identificador do plano agendado é inválido.");
    public static Error OffboardingNecessario => new("assinatura_treinador.offboarding_necessario", "Não é possível cancelar o plano com alunos ativos ou pendentes vinculados. Desvincule os alunos antes de cancelar.");
}
