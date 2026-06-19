namespace forzion.tech.Domain.Shared.Errors;

public static class TreinoAlunoErrors
{
    public static Error TreinoInvalido => Error.Validation("treino_aluno.treino_invalido", "O treino é inválido.");
    public static Error AlunoInvalido => Error.Validation("treino_aluno.aluno_invalido", "O aluno é inválido.");
}
