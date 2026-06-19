namespace forzion.tech.Domain.Shared.Errors;

public static class ContaRecebimentoErrors
{
    public static Error TreinadorIdInvalido => Error.Validation("conta_recebimento.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error StripeAccountIdInvalido => Error.Validation("conta_recebimento.stripe_account_id_invalido", "O identificador da conta Stripe é inválido.");
    public static Error SemContaStripe => Error.Business("conta_recebimento.sem_conta_stripe", "O treinador não possui conta Stripe configurada.");
}
