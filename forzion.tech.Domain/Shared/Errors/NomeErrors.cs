namespace forzion.tech.Domain.Shared.Errors;

public static class NomeErrors
{
    public static Error Obrigatorio => new("nome.obrigatorio", "O nome é obrigatório.");
    public static Error MuitoLongo => new("nome.muito_longo", "O nome deve ter no máximo 100 caracteres.");
}
