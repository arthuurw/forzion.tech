namespace forzion.tech.Domain.Shared.Errors;

public static class LeadConviteErrors
{
    public static Error TokenHashObrigatorio => Error.Validation("lead_convite.token_hash_obrigatorio", "O hash do token é obrigatório.");
    public static Error ExpiracaoNaoFutura => Error.Validation("lead_convite.expiracao_nao_futura", "A expiração do convite deve ser no futuro.");
    public static Error JaUtilizado => Error.Business("lead_convite.ja_utilizado", "Este convite já foi utilizado.");
    public static Error JaInvalidado => Error.Business("lead_convite.ja_invalidado", "Este convite não está mais disponível.");
    public static Error Expirado => Error.Business("lead_convite.expirado", "Este convite expirou.");
}
