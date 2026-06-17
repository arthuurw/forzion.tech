using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class MfaChallenge
{
    public const int MaximoTentativas = 5;

    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public string CodigoHash { get; private set; } = string.Empty;
    public MfaProposito Proposito { get; private set; }
    public DateTime ExpiraEm { get; private set; }
    public DateTime? UsadoEm { get; private set; }
    public int Tentativas { get; private set; }
    public DateTime CriadoEm { get; private set; }

    private MfaChallenge() { }

    public static Result<MfaChallenge> Criar(Guid contaId, string codigoHash, MfaProposito proposito, DateTime expiraEm, DateTime agora)
    {
        if (contaId == Guid.Empty)
            return Result.Failure<MfaChallenge>(MfaErrors.ContaIdInvalido);

        if (string.IsNullOrWhiteSpace(codigoHash))
            return Result.Failure<MfaChallenge>(MfaErrors.CodigoHashObrigatorio);

        if (expiraEm <= agora)
            return Result.Failure<MfaChallenge>(MfaErrors.ExpiracaoNaoFutura);

        return Result.Success(new MfaChallenge
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            CodigoHash = codigoHash,
            Proposito = proposito,
            ExpiraEm = expiraEm,
            CriadoEm = agora
        });
    }

    public bool Expirado(DateTime agora) => agora >= ExpiraEm;

    public bool Bloqueado => Tentativas >= MaximoTentativas;

    public Result Validar(DateTime agora)
    {
        if (UsadoEm.HasValue)
            return Result.Failure(MfaErrors.ChallengeJaUtilizado);

        if (Expirado(agora))
            return Result.Failure(MfaErrors.ChallengeExpirado);

        if (Bloqueado)
            return Result.Failure(MfaErrors.ChallengeBloqueado);

        return Result.Success();
    }

    public void RegistrarTentativa() => Tentativas++;

    public Result MarcarUsado(DateTime agora)
    {
        if (UsadoEm.HasValue)
            return Result.Failure(MfaErrors.ChallengeJaUtilizado);

        UsadoEm = agora;
        return Result.Success();
    }
}
