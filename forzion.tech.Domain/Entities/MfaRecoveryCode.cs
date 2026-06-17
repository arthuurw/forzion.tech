using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class MfaRecoveryCode
{
    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public string CodigoHash { get; private set; } = string.Empty;
    public DateTime? UsadoEm { get; private set; }
    public DateTime CriadoEm { get; private set; }

    private MfaRecoveryCode() { }

    public static Result<MfaRecoveryCode> Criar(Guid contaId, string codigoHash, DateTime agora)
    {
        if (contaId == Guid.Empty)
            return Result.Failure<MfaRecoveryCode>(MfaErrors.ContaIdInvalido);

        if (string.IsNullOrWhiteSpace(codigoHash))
            return Result.Failure<MfaRecoveryCode>(MfaErrors.CodigoHashObrigatorio);

        return Result.Success(new MfaRecoveryCode
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            CodigoHash = codigoHash,
            CriadoEm = agora
        });
    }

    public bool Disponivel => !UsadoEm.HasValue;

    public Result MarcarUsado(DateTime agora)
    {
        if (UsadoEm.HasValue)
            return Result.Failure(MfaErrors.RecoveryJaUtilizado);

        UsadoEm = agora;
        return Result.Success();
    }
}
