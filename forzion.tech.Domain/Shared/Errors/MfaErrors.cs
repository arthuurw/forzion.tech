namespace forzion.tech.Domain.Shared.Errors;

public static class MfaErrors
{
    public static Error ContaIdInvalido => new("mfa.conta_id_invalido", "O identificador da conta é inválido.");
    public static Error SecretObrigatorio => new("mfa.secret_obrigatorio", "O segredo TOTP é obrigatório.");
    public static Error JaConfirmado => new("mfa.ja_confirmado", "A autenticação em dois fatores já foi confirmada.");
    public static Error NaoHabilitado => new("mfa.nao_habilitado", "A autenticação em dois fatores não está habilitada.");
    public static Error CodigoReutilizado => new("mfa.codigo_reutilizado", "Este código já foi utilizado.");

    public static Error CodigoHashObrigatorio => new("mfa.codigo_hash_obrigatorio", "O hash do código é obrigatório.");
    public static Error ExpiracaoNaoFutura => new("mfa.expiracao_nao_futura", "A data de expiração deve ser futura.");
    public static Error RecoveryJaUtilizado => new("mfa.recovery_ja_utilizado", "O código de recuperação já foi utilizado.");
    public static Error ChallengeJaUtilizado => new("mfa.challenge_ja_utilizado", "O código já foi utilizado.");
    public static Error ChallengeExpirado => new("mfa.challenge_expirado", "O código expirou. Solicite um novo.");
    public static Error ChallengeBloqueado => new("mfa.challenge_bloqueado", "Muitas tentativas. Solicite um novo código.");

    public static Error TokenHashObrigatorio => new("mfa.token_hash_obrigatorio", "O hash do dispositivo é obrigatório.");
    public static Error DispositivoExpirado => new("mfa.dispositivo_expirado", "O dispositivo confiável expirou.");
    public static Error DispositivoJaRevogado => new("mfa.dispositivo_ja_revogado", "O dispositivo já foi revogado.");
}
