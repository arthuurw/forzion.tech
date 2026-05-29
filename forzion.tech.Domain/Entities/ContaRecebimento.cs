using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class ContaRecebimento
{
    public Guid Id { get; private set; }
    public Guid TreinadorId { get; private set; }
    public string? StripeConnectAccountId { get; private set; }
    public bool OnboardingCompleto { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public bool Configurada => !string.IsNullOrWhiteSpace(StripeConnectAccountId);

    private ContaRecebimento() { }

    public static Result<ContaRecebimento> Criar(Guid treinadorId, DateTime agora)
    {
        if (treinadorId == Guid.Empty)
            return Result.Failure<ContaRecebimento>(ContaRecebimentoErrors.TreinadorIdInvalido);

        var conta = new ContaRecebimento
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            OnboardingCompleto = false,
            CreatedAt = agora
        };

        return Result.Success(conta);
    }

    public Result ConfigurarStripeConnect(string accountId, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            return Result.Failure(ContaRecebimentoErrors.StripeAccountIdInvalido);

        StripeConnectAccountId = accountId;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result ConfirmarOnboarding(DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(StripeConnectAccountId))
            return Result.Failure(ContaRecebimentoErrors.SemContaStripe);

        OnboardingCompleto = true;
        UpdatedAt = agora;
        return Result.Success();
    }
}
