namespace forzion.tech.Domain.Shared.Errors;

public static class ContaRecebimentoErrors
{
    public static Error TreinadorIdInvalido => new("conta_recebimento.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error StripeAccountIdInvalido => new("conta_recebimento.stripe_account_id_invalido", "O identificador da conta Stripe é inválido.");
    public static Error SemContaStripe => new("conta_recebimento.sem_conta_stripe", "O treinador não possui conta Stripe configurada.");
}
