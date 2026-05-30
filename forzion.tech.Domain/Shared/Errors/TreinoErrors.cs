namespace forzion.tech.Domain.Shared.Errors;

public static class TreinoErrors
{
    public static Error NomeObrigatorio => new("treino.nome_obrigatorio", "O nome é obrigatório.");
    public static Error NomeMuitoLongo => new("treino.nome_muito_longo", "O nome deve ter no máximo 100 caracteres.");
    public static Error NomeVazio => new("treino.nome_vazio", "O nome não pode ser vazio.");
    public static Error TreinadorInvalido => new("treino.treinador_invalido", "O treinador é inválido.");
    public static Error DataFimAnteriorInicio => new("treino.data_fim_anterior_inicio", "A data de fim deve ser posterior à data de início.");
    public static Error TreinadorDestinoInvalido => new("treino.treinador_destino_invalido", "O treinador de destino é inválido.");
    public static Error ExercicioNaoEncontrado => new("treino.exercicio_nao_encontrado", "Exercício não encontrado no treino.");
    public static Error TreinoJaExecutado => new("treino.ja_executado", "Treino já executado não pode ser alterado.");

    public static Error TreinoInvalido => new("treino_exercicio.treino_invalido", "O treino é inválido.");
    public static Error ExercicioInvalido => new("treino_exercicio.exercicio_invalido", "O exercício é inválido.");
    public static Error ObservacaoMuitoLonga => new("treino_exercicio.observacao_muito_longa", "A observação deve ter no máximo 500 caracteres.");
    public static Error PeloMenosUmGrupoSeries => new("treino_exercicio.pelo_menos_um_grupo_series", "O exercício deve ter pelo menos um grupo de séries.");

    public static Error QuantidadeInvalida => new("serie_config.quantidade_invalida", "A quantidade de séries deve ser maior que zero.");
    public static Error RepeticoesMinInvalida => new("serie_config.repeticoes_min_invalida", "O número mínimo de repetições deve ser maior que zero.");
    public static Error RepeticoesMaxMenorQueMin => new("serie_config.repeticoes_max_menor_que_min", "O máximo de repetições não pode ser menor que o mínimo.");
    public static Error CargaNegativa => new("serie_config.carga_negativa", "A carga não pode ser negativa.");
    public static Error DescansoNegativo => new("serie_config.descanso_negativo", "O descanso não pode ser negativo.");
}
