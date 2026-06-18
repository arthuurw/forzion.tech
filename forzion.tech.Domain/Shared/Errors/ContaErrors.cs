namespace forzion.tech.Domain.Shared.Errors;

public static class ContaErrors
{
    public static Error PasswordHashObrigatorio => Error.Validation("conta.password_hash_obrigatorio", "O hash da senha é obrigatório.");
    public static Error JaAnonimizada => Error.Conflict("conta.ja_anonimizada", "A conta já foi anonimizada.");
    public static Error OffboardingNecessario => Error.Conflict("conta.offboarding_necessario", "O treinador possui vínculos ativos. Encerre todos os vínculos antes de anonimizar a conta.");
}
