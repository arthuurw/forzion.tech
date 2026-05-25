using forzion.tech.Domain.Exceptions;

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

    public static ContaRecebimento Criar(Guid treinadorId, DateTime agora)
    {
        if (treinadorId == Guid.Empty)
            throw new DomainException("O identificador do treinador é inválido.");

        return new ContaRecebimento
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            OnboardingCompleto = false,
            CreatedAt = agora
        };
    }

    public void ConfigurarStripeConnect(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            throw new DomainException("O identificador da conta Stripe é inválido.");

        StripeConnectAccountId = accountId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConfirmarOnboarding()
    {
        if (string.IsNullOrWhiteSpace(StripeConnectAccountId))
            throw new DomainException("O treinador não possui conta Stripe configurada.");

        OnboardingCompleto = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
