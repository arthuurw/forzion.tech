namespace forzion.tech.Domain.Shared.Errors;

public static class TreinoErrors
{
    public static Error NomeObrigatorio => Error.Validation("treino.nome_obrigatorio", "O nome é obrigatório.");
    public static Error NomeMuitoLongo => Error.Validation("treino.nome_muito_longo", "O nome deve ter no máximo 100 caracteres.");
    public static Error NomeVazio => Error.Validation("treino.nome_vazio", "O nome não pode ser vazio.");
    public static Error TreinadorInvalido => Error.Validation("treino.treinador_invalido", "O treinador é inválido.");
    public static Error DataFimAnteriorInicio => Error.Validation("treino.data_fim_anterior_inicio", "A data de fim deve ser posterior à data de início.");
    public static Error TreinadorDestinoInvalido => Error.Validation("treino.treinador_destino_invalido", "O treinador de destino é inválido.");
    public static Error ExercicioNaoEncontrado => Error.NotFound("treino.exercicio_nao_encontrado", "Exercício não encontrado no treino.");
    public static Error TreinoJaExecutado => Error.Conflict("treino.ja_executado", "Treino já executado não pode ser alterado.");

    public static Error TreinoInvalido => Error.Validation("treino_exercicio.treino_invalido", "O treino é inválido.");
    public static Error ExercicioInvalido => Error.Validation("treino_exercicio.exercicio_invalido", "O exercício é inválido.");
    public static Error ObservacaoMuitoLonga => Error.Validation("treino_exercicio.observacao_muito_longa", "A observação deve ter no máximo 500 caracteres.");
    public static Error PeloMenosUmGrupoSeries => Error.Validation("treino_exercicio.pelo_menos_um_grupo_series", "O exercício deve ter pelo menos um grupo de séries.");

    public static Error QuantidadeInvalida => Error.Validation("serie_config.quantidade_invalida", "A quantidade de séries deve ser maior que zero.");
    public static Error RepeticoesMinInvalida => Error.Validation("serie_config.repeticoes_min_invalida", "O número mínimo de repetições deve ser maior que zero.");
    public static Error RepeticoesMaxMenorQueMin => Error.Validation("serie_config.repeticoes_max_menor_que_min", "O máximo de repetições não pode ser menor que o mínimo.");
    public static Error CargaNegativa => Error.Validation("serie_config.carga_negativa", "A carga não pode ser negativa.");
    public static Error DescansoNegativo => Error.Validation("serie_config.descanso_negativo", "O descanso não pode ser negativo.");
}
