namespace forzion.tech.Domain.Shared.Errors;

public static class ContaErrors
{
    public static Error PasswordHashObrigatorio => new("conta.password_hash_obrigatorio", "O hash da senha é obrigatório.");
    public static Error JaAnonimizada => new("conta.ja_anonimizada", "A conta já foi anonimizada.");
}
