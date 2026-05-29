namespace forzion.tech.Domain.Shared.Errors;

public static class EmailErrors
{
    public static Error Obrigatorio => new("email.obrigatorio", "O e-mail é obrigatório.");
    public static Error MuitoLongo => new("email.muito_longo", "O e-mail deve ter no máximo 256 caracteres.");
    public static Error Invalido => new("email.invalido", "O e-mail informado é inválido.");
}
