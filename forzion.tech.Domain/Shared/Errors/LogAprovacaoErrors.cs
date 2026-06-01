namespace forzion.tech.Domain.Shared.Errors;

public static class LogAprovacaoErrors
{
    public static Error RealizadoPorIdInvalido => new("log_aprovacao.realizado_por_id_invalido", "O identificador de quem realizou a ação é inválido.");
    public static Error EntidadeIdInvalido => new("log_aprovacao.entidade_id_invalido", "O identificador da entidade é inválido.");
    public static Error EntidadeTipoObrigatorio => new("log_aprovacao.entidade_tipo_obrigatorio", "O tipo da entidade é obrigatório.");
    public static Error ObservacaoMuitoLonga => new("log_aprovacao.observacao_muito_longa", "A observação deve ter no máximo 500 caracteres.");

    public static Error NivelObrigatorio => new("error_log_entry.nivel_obrigatorio", "O nível é obrigatório.");
    public static Error OrigemObrigatoria => new("error_log_entry.origem_obrigatoria", "A origem é obrigatória.");
}
