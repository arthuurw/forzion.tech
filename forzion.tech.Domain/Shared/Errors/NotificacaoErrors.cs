namespace forzion.tech.Domain.Shared.Errors;

public static class NotificacaoErrors
{
    public static Error DestinatarioInvalido => Error.Validation("notificacao.destinatario_invalido", "O destinatário é inválido.");
    public static Error TituloObrigatorio => Error.Validation("notificacao.titulo_obrigatorio", "O título é obrigatório.");
    public static Error TituloMuitoLongo => Error.Validation("notificacao.titulo_muito_longo", "O título deve ter no máximo 120 caracteres.");
    public static Error CorpoObrigatorio => Error.Validation("notificacao.corpo_obrigatorio", "O corpo é obrigatório.");
    public static Error CorpoMuitoLongo => Error.Validation("notificacao.corpo_muito_longo", "O corpo deve ter no máximo 500 caracteres.");
}
