namespace forzion.tech.Domain.Shared.Errors;

public static class ContaErrors
{
    public static Error PasswordHashObrigatorio => new("conta.password_hash_obrigatorio", "O hash da senha é obrigatório.");
}
