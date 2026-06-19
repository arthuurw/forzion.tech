namespace forzion.tech.Domain.Shared.Errors;

public static class NomeErrors
{
    public static Error Obrigatorio => Error.Validation("nome.obrigatorio", "O nome é obrigatório.");
    public static Error MuitoLongo => Error.Validation("nome.muito_longo", "O nome deve ter no máximo 100 caracteres.");
}
