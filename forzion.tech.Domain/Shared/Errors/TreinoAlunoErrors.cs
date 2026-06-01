namespace forzion.tech.Domain.Shared.Errors;

public static class TreinoAlunoErrors
{
    public static Error TreinoInvalido => new("treino_aluno.treino_invalido", "O treino é inválido.");
    public static Error AlunoInvalido => new("treino_aluno.aluno_invalido", "O aluno é inválido.");
}
