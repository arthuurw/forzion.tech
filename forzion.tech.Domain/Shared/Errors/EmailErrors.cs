namespace forzion.tech.Domain.Shared.Errors;

public static class EmailErrors
{
    public static Error Obrigatorio => Error.Validation("email.obrigatorio", "O e-mail é obrigatório.");
    public static Error MuitoLongo => Error.Validation("email.muito_longo", "O e-mail deve ter no máximo 256 caracteres.");
    public static Error Invalido => Error.Validation("email.invalido", "O e-mail informado é inválido.");
}
