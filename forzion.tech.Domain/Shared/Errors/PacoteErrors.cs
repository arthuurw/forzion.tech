namespace forzion.tech.Domain.Shared.Errors;

public static class PacoteErrors
{
    public static Error TreinadorIdInvalido => new("pacote.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error NomeObrigatorio => new("pacote.nome_obrigatorio", "O nome é obrigatório.");
    public static Error NomeVazio => new("pacote.nome_vazio", "O nome não pode ser vazio.");
    public static Error NomeMuitoLongo => new("pacote.nome_muito_longo", "O nome deve ter no máximo 100 caracteres.");
    public static Error PrecoNegativo => new("pacote.preco_negativo", "O preço não pode ser negativo.");
    public static Error DescricaoMuitoLonga => new("pacote.descricao_muito_longa", "A descrição deve ter no máximo 500 caracteres.");
}
