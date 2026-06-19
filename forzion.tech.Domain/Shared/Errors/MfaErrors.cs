namespace forzion.tech.Domain.Shared.Errors;

public static class MfaErrors
{
    public static Error ContaIdInvalido => Error.Validation("mfa.conta_id_invalido", "O identificador da conta é inválido.");
    public static Error SecretObrigatorio => Error.Validation("mfa.secret_obrigatorio", "O segredo TOTP é obrigatório.");
    public static Error JaConfirmado => Error.Conflict("mfa.ja_confirmado", "A autenticação em dois fatores já foi confirmada.");
    public static Error NaoHabilitado => Error.Conflict("mfa.nao_habilitado", "A autenticação em dois fatores não está habilitada.");
    public static Error CodigoReutilizado => Error.Conflict("mfa.codigo_reutilizado", "Este código já foi utilizado.");
    public static Error EnrollNaoIniciado => Error.Business("mfa.enroll_nao_iniciado", "Inicie a configuração da autenticação em dois fatores antes de confirmar.");
    public static Error CodigoInvalido => Error.Business("mfa.codigo_invalido", "O código informado é inválido.");

    public static Error CodigoHashObrigatorio => Error.Validation("mfa.codigo_hash_obrigatorio", "O hash do código é obrigatório.");
    public static Error ExpiracaoNaoFutura => Error.Validation("mfa.expiracao_nao_futura", "A data de expiração deve ser futura.");
    public static Error RecoveryJaUtilizado => Error.Conflict("mfa.recovery_ja_utilizado", "O código de recuperação já foi utilizado.");
    public static Error ChallengeJaUtilizado => Error.Conflict("mfa.challenge_ja_utilizado", "O código já foi utilizado.");
    public static Error ChallengeExpirado => Error.Business("mfa.challenge_expirado", "O código expirou. Solicite um novo.");
    public static Error ChallengeBloqueado => Error.Business("mfa.challenge_bloqueado", "Muitas tentativas. Solicite um novo código.");

    public static Error TokenHashObrigatorio => Error.Validation("mfa.token_hash_obrigatorio", "O hash do dispositivo é obrigatório.");
    public static Error DispositivoExpirado => Error.Business("mfa.dispositivo_expirado", "O dispositivo confiável expirou.");
    public static Error DispositivoJaRevogado => Error.Conflict("mfa.dispositivo_ja_revogado", "O dispositivo já foi revogado.");
}
