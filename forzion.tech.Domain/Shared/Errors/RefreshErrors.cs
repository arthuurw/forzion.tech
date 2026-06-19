namespace forzion.tech.Domain.Shared.Errors;

public static class RefreshErrors
{
    public static Error ContaIdInvalido => Error.Validation("refresh.conta_id_invalido", "O identificador da conta é inválido.");
    public static Error FamiliaIdInvalido => Error.Validation("refresh.familia_id_invalido", "O identificador da família é inválido.");
    public static Error TokenHashObrigatorio => Error.Validation("refresh.token_hash_obrigatorio", "O hash do token é obrigatório.");
    public static Error ExpiracaoNaoFutura => Error.Validation("refresh.expiracao_nao_futura", "A data de expiração deve ser futura.");
    public static Error AbsolutoNaoFuturo => Error.Validation("refresh.absoluto_nao_futuro", "O teto absoluto da sessão deve ser futuro.");
    public static Error FamiliaJaRevogada => Error.Conflict("refresh.familia_ja_revogada", "A família já foi revogada.");
    public static Error TokenJaUtilizado => Error.Conflict("refresh.token_ja_utilizado", "O token já foi utilizado.");
    public static Error SucessorInvalido => Error.Validation("refresh.sucessor_invalido", "O identificador do token sucessor é inválido.");
    public static Error SessaoInvalida => Error.Business("refresh.sessao_invalida", "Sessão inválida ou expirada.");
}
