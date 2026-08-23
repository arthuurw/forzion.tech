namespace forzion.tech.Domain.Shared.Errors;

public static class PacoteErrors
{
    public static Error TreinadorIdInvalido => Error.Validation("pacote.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error NomeObrigatorio => Error.Validation("pacote.nome_obrigatorio", "O nome é obrigatório.");
    public static Error NomeVazio => Error.Validation("pacote.nome_vazio", "O nome não pode ser vazio.");
    public static Error NomeMuitoLongo => Error.Validation("pacote.nome_muito_longo", "O nome deve ter no máximo 100 caracteres.");
    public static Error PrecoNegativo => Error.Validation("pacote.preco_negativo", "O preço não pode ser negativo.");
    public static Error DescricaoMuitoLonga => Error.Validation("pacote.descricao_muito_longa", "A descrição deve ter no máximo 500 caracteres.");
    public static Error NaoEncontrado => Error.NotFound("pacote.nao_encontrado", "Pacote não encontrado.");
    public static Error NaoPertenceTreinador => Error.Business("pacote.nao_pertence_treinador", "Pacote não pertence ao treinador informado.");
    public static Error CategoriaObrigatoriaParaPublico => Error.Validation("pacote.categoria_obrigatoria_para_publico", "A categoria é obrigatória para tornar o pacote público.");
    public static Error CategoriaMuitoLonga => Error.Validation("pacote.categoria_muito_longa", "A categoria deve ter no máximo 100 caracteres.");
    public static Error DuracaoMinutosInvalida => Error.Validation("pacote.duracao_minutos_invalida", "A duração em minutos deve ser maior que zero.");
    public static Error CapacidadeMaximaInvalida => Error.Validation("pacote.capacidade_maxima_invalida", "A capacidade máxima deve ser maior que zero.");
}
