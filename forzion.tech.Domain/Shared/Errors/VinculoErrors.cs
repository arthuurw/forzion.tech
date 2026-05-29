namespace forzion.tech.Domain.Shared.Errors;

public static class VinculoErrors
{
    public static Error TreinadorIdInvalido => new("vinculo.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error AlunoIdInvalido => new("vinculo.aluno_id_invalido", "O identificador do aluno é inválido.");
    public static Error PacoteIdInvalido => new("vinculo.pacote_id_invalido", "O identificador do pacote é inválido.");
    public static Error NaoAguardandoAprovacao => new("vinculo.nao_aguardando_aprovacao", "Apenas vínculos aguardando aprovação podem ser aprovados.");
    public static Error JaInativo => new("vinculo.ja_inativo", "O vínculo já está inativo.");
}
