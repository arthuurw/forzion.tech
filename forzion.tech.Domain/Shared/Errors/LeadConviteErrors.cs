namespace forzion.tech.Domain.Shared.Errors;

public static class LeadConviteErrors
{
    public static Error TokenHashObrigatorio => Error.Validation("lead_convite.token_hash_obrigatorio", "O hash do token é obrigatório.");
    public static Error ExpiracaoNaoFutura => Error.Validation("lead_convite.expiracao_nao_futura", "A expiração do convite deve ser no futuro.");
    public static Error JaUtilizado => Error.Business("lead_convite.ja_utilizado", "Este convite já foi utilizado.");
    public static Error JaInvalidado => Error.Business("lead_convite.ja_invalidado", "Este convite não está mais disponível.");
    public static Error Expirado => Error.Business("lead_convite.expirado", "Este convite expirou.");
    public static Error ContatoNaoSuportaConvite => Error.Business("lead_convite.contato_nao_suporta_convite", "O convite exige contato de e-mail (ou WhatsApp, quando disponível) — este lead só tem telefone.");
    public static Error TokenInvalido => Error.NotFound("lead_convite.token_invalido", "Convite não encontrado ou não é mais válido.");

    public static Error Aguarde(DateTime liberadoEm) =>
        Error.Business("lead_convite.aguarde", $"Aguarde até {liberadoEm:HH:mm} para reenviar o convite a este lead.");

    public static Error TetoDiarioAtingido(DateTime liberadoEm) =>
        Error.Business("lead_convite.teto_diario_atingido", $"Teto diário de convites para este lead atingido. Tente novamente após {liberadoEm:dd/MM/yyyy HH:mm}.");
}
