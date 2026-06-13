namespace forzion.tech.Domain.Shared.Errors;

public static class RefreshErrors
{
    public static Error ContaIdInvalido => new("refresh.conta_id_invalido", "O identificador da conta é inválido.");
    public static Error FamiliaIdInvalido => new("refresh.familia_id_invalido", "O identificador da família é inválido.");
    public static Error TokenHashObrigatorio => new("refresh.token_hash_obrigatorio", "O hash do token é obrigatório.");
    public static Error ExpiracaoNaoFutura => new("refresh.expiracao_nao_futura", "A data de expiração deve ser futura.");
    public static Error AbsolutoNaoFuturo => new("refresh.absoluto_nao_futuro", "O teto absoluto da sessão deve ser futuro.");
    public static Error FamiliaJaRevogada => new("refresh.familia_ja_revogada", "A família já foi revogada.");
    public static Error TokenJaUtilizado => new("refresh.token_ja_utilizado", "O token já foi utilizado.");
    public static Error SucessorInvalido => new("refresh.sucessor_invalido", "O identificador do token sucessor é inválido.");
    public static Error SessaoInvalida => new("refresh.sessao_invalida", "Sessão inválida ou expirada.");
}
