namespace forzion.tech.Domain.Shared.Errors;

public static class VinculoErrors
{
    public static Error TreinadorIdInvalido => Error.Validation("vinculo.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error AlunoIdInvalido => Error.Validation("vinculo.aluno_id_invalido", "O identificador do aluno é inválido.");
    public static Error PacoteIdInvalido => Error.Validation("vinculo.pacote_id_invalido", "O identificador do pacote é inválido.");
    public static Error NaoAguardandoAprovacao => Error.Conflict("vinculo.nao_aguardando_aprovacao", "Apenas vínculos aguardando aprovação podem ser aprovados.");
    public static Error JaInativo => Error.Conflict("vinculo.ja_inativo", "O vínculo já está inativo.");
}
