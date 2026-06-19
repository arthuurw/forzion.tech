namespace forzion.tech.Domain.Shared.Errors;

public static class GrupoMuscularErrors
{
    public static Error NomeObrigatorio => Error.Validation("grupo_muscular.nome_obrigatorio", "O nome do grupo muscular é obrigatório.");
    public static Error NomeMuitoLongo => Error.Validation("grupo_muscular.nome_muito_longo", "O nome do grupo muscular deve ter no máximo 50 caracteres.");
    public static Error NomeVazio => Error.Validation("grupo_muscular.nome_vazio", "O nome do grupo muscular não pode ser vazio.");
}
