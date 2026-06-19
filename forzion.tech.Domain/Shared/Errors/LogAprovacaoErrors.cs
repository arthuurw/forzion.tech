namespace forzion.tech.Domain.Shared.Errors;

public static class LogAprovacaoErrors
{
    public static Error RealizadoPorIdInvalido => Error.Validation("log_aprovacao.realizado_por_id_invalido", "O identificador de quem realizou a ação é inválido.");
    public static Error EntidadeIdInvalido => Error.Validation("log_aprovacao.entidade_id_invalido", "O identificador da entidade é inválido.");
    public static Error EntidadeTipoObrigatorio => Error.Validation("log_aprovacao.entidade_tipo_obrigatorio", "O tipo da entidade é obrigatório.");
    public static Error ObservacaoMuitoLonga => Error.Validation("log_aprovacao.observacao_muito_longa", "A observação deve ter no máximo 500 caracteres.");

    public static Error NivelObrigatorio => Error.Validation("error_log_entry.nivel_obrigatorio", "O nível é obrigatório.");
    public static Error OrigemObrigatoria => Error.Validation("error_log_entry.origem_obrigatoria", "A origem é obrigatória.");
}
