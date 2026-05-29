namespace forzion.tech.Domain.Shared.Errors;

public static class ExecucaoTreinoErrors
{
    public static Error TreinoInvalido => new("execucao_treino.treino_invalido", "O treino é inválido.");
    public static Error AlunoInvalido => new("execucao_treino.aluno_invalido", "O aluno é inválido.");
    public static Error DataExecucaoInvalida => new("execucao_treino.data_execucao_invalida", "A data de execução é inválida.");
    public static Error ObservacaoMuitoLonga => new("execucao_treino.observacao_muito_longa", "A observação deve ter no máximo 500 caracteres.");

    public static Error ExecucaoInvalida => new("execucao_exercicio.execucao_invalida", "A execução é inválida.");
    public static Error ExercicioTreinoInvalido => new("execucao_exercicio.exercicio_treino_invalido", "O exercício do treino é inválido.");
    public static Error SeriesInvalidas => new("execucao_exercicio.series_invalidas", "O número de séries deve ser maior que zero.");
    public static Error RepeticoesInvalidas => new("execucao_exercicio.repeticoes_invalidas", "O número de repetições deve ser maior que zero.");
    public static Error CargaNegativa => new("execucao_exercicio.carga_negativa", "A carga não pode ser negativa.");
    public static Error ExercicioObservacaoMuitoLonga => new("execucao_exercicio.observacao_muito_longa", "A observação deve ter no máximo 500 caracteres.");
}
