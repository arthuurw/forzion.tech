namespace forzion.tech.Domain.Shared.Errors;

public static class TelefoneErrors
{
    public static Error MuitoLongo => new("telefone.muito_longo", "O telefone deve ter no máximo 20 caracteres.");
}
