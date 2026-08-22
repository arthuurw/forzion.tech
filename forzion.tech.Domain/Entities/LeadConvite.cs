using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class LeadConvite
{
    public Guid Id { get; private set; }
    public Guid LeadId { get; private set; }
    public Guid TreinadorId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiraEm { get; private set; }
    public DateTime? UsadoEm { get; private set; }
    public DateTime? InvalidadoEm { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private LeadConvite() { }

    public static Result<LeadConvite> Criar(Guid leadId, Guid treinadorId, string tokenHash, DateTime expiraEm, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            return Result.Failure<LeadConvite>(LeadConviteErrors.TokenHashObrigatorio);

        if (expiraEm <= agora)
            return Result.Failure<LeadConvite>(LeadConviteErrors.ExpiracaoNaoFutura);

        return Result.Success(new LeadConvite
        {
            Id = Guid.NewGuid(),
            LeadId = leadId,
            TreinadorId = treinadorId,
            TokenHash = tokenHash,
            ExpiraEm = expiraEm,
            CreatedAt = agora
        });
    }

    public Result Consumir(DateTime agora)
    {
        if (UsadoEm.HasValue)
            return Result.Failure(LeadConviteErrors.JaUtilizado);

        if (InvalidadoEm.HasValue)
            return Result.Failure(LeadConviteErrors.JaInvalidado);

        if (agora >= ExpiraEm)
            return Result.Failure(LeadConviteErrors.Expirado);

        UsadoEm = agora;
        return Result.Success();
    }

    public Result Invalidar(DateTime agora)
    {
        InvalidadoEm ??= agora;
        return Result.Success();
    }
}
