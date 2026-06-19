namespace forzion.tech.Domain.Shared.Errors;

public static class TokenErrors
{
    public static Error ContaIdInvalido => Error.Validation("token.conta_id_invalido", "O identificador da conta é inválido.");
    public static Error TokenHashObrigatorio => Error.Validation("token.token_hash_obrigatorio", "O hash do token é obrigatório.");
    public static Error ExpiracaoNaoFutura => Error.Validation("token.expiracao_nao_futura", "A data de expiração deve ser futura.");
    public static Error JaUtilizado => Error.Conflict("token.ja_utilizado", "O token já foi utilizado.");
    public static Error JtiInvalido => Error.Validation("token.jti_invalido", "O identificador do token é inválido.");
    public static Error ExpiracaoNaoFuturaRevogado => Error.Validation("token.expiracao_nao_futura_revogado", "A data de expiração do token deve ser futura.");
}
