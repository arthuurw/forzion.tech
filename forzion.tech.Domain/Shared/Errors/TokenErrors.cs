namespace forzion.tech.Domain.Shared.Errors;

public static class TokenErrors
{
    public static Error ContaIdInvalido => new("token.conta_id_invalido", "O identificador da conta é inválido.");
    public static Error TokenHashObrigatorio => new("token.token_hash_obrigatorio", "O hash do token é obrigatório.");
    public static Error ExpiracaoNaoFutura => new("token.expiracao_nao_futura", "A data de expiração deve ser futura.");
    public static Error JaUtilizado => new("token.ja_utilizado", "O token já foi utilizado.");
    public static Error JtiInvalido => new("token.jti_invalido", "O identificador do token é inválido.");
    public static Error ExpiracaoNaoFuturaRevogado => new("token.expiracao_nao_futura_revogado", "A data de expiração do token deve ser futura.");
}
