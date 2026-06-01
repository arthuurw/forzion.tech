namespace forzion.tech.Domain.Shared.Errors;

public static class ExercicioErrors
{
    public static Error NomeObrigatorio => new("exercicio.nome_obrigatorio", "O nome é obrigatório.");
    public static Error NomeMuitoLongo => new("exercicio.nome_muito_longo", "O nome deve ter no máximo 100 caracteres.");
    public static Error NomeVazio => new("exercicio.nome_vazio", "O nome não pode ser vazio.");
    public static Error GrupoMuscularObrigatorio => new("exercicio.grupo_muscular_obrigatorio", "O grupo muscular é obrigatório.");
    public static Error TreinadorIdInvalido => new("exercicio.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error DescricaoMuitoLonga => new("exercicio.descricao_muito_longa", "A descrição deve ter no máximo 500 caracteres.");
}
