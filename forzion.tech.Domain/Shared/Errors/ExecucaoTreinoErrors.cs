namespace forzion.tech.Domain.Shared.Errors;

public static class ExecucaoTreinoErrors
{
    public static Error TreinoInvalido => Error.Validation("execucao_treino.treino_invalido", "O treino é inválido.");
    public static Error AlunoInvalido => Error.Validation("execucao_treino.aluno_invalido", "O aluno é inválido.");
    public static Error DataExecucaoInvalida => Error.Validation("execucao_treino.data_execucao_invalida", "A data de execução é inválida.");
    public static Error ObservacaoMuitoLonga => Error.Validation("execucao_treino.observacao_muito_longa", "A observação deve ter no máximo 500 caracteres.");

    public static Error ExecucaoInvalida => Error.Validation("execucao_exercicio.execucao_invalida", "A execução é inválida.");
    public static Error ExercicioTreinoInvalido => Error.Validation("execucao_exercicio.exercicio_treino_invalido", "O exercício do treino é inválido.");
    public static Error SeriesInvalidas => Error.Validation("execucao_exercicio.series_invalidas", "O número de séries deve ser maior que zero.");
    public static Error RepeticoesInvalidas => Error.Validation("execucao_exercicio.repeticoes_invalidas", "O número de repetições deve ser maior que zero.");
    public static Error CargaNegativa => Error.Validation("execucao_exercicio.carga_negativa", "A carga não pode ser negativa.");
    public static Error ExercicioObservacaoMuitoLonga => Error.Validation("execucao_exercicio.observacao_muito_longa", "A observação deve ter no máximo 500 caracteres.");
}
