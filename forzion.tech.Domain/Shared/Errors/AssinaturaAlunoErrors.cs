namespace forzion.tech.Domain.Shared.Errors;

public static class AssinaturaAlunoErrors
{
    public static Error VinculoIdInvalido => new("assinatura_aluno.vinculo_id_invalido", "O identificador do vínculo é inválido.");
    public static Error PacoteIdInvalido => new("assinatura_aluno.pacote_id_invalido", "O identificador do pacote é inválido.");
    public static Error TreinadorIdInvalido => new("assinatura_aluno.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error AlunoIdInvalido => new("assinatura_aluno.aluno_id_invalido", "O identificador do aluno é inválido.");
    public static Error ValorInvalido => new("assinatura_aluno.valor_invalido", "O valor da assinatura deve ser maior que zero.");
    public static Error CanceladaNaoAtivavel => new("assinatura_aluno.cancelada_nao_ativavel", "AssinaturaAluno cancelada não pode ser ativada.");
    public static Error ApenasAtivasInadimplentes => new("assinatura_aluno.apenas_ativas_inadimplentes", "Apenas assinaturas ativas podem ser marcadas como inadimplentes.");
    public static Error JaCancelada => new("assinatura_aluno.ja_cancelada", "A assinatura já está cancelada.");
    public static Error ProximaCobrancaNaoFutura => new("assinatura_aluno.proxima_cobranca_nao_futura", "A data da próxima cobrança deve ser futura.");
    public static Error InadimplenteDeveUsarRegularizacao => Error.Conflict("assinatura_aluno.inadimplente_deve_usar_regularizacao", "Assinatura inadimplente não pode ser ativada diretamente; use RegistrarPagamentoRegularizado.");
    public static Error NaoEncontrada => Error.NotFound("assinatura_aluno.nao_encontrada", "AssinaturaAluno não encontrada.");
    public static Error NaoEncontradaParaCancelar => new("assinatura_aluno.nao_encontrada_para_cancelar", "Nenhuma assinatura ativa encontrada para cancelar.");
}
