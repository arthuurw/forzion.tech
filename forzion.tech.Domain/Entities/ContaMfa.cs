using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class ContaMfa
{
    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public string? TotpSecretCifrado { get; private set; }
    public bool Habilitado { get; private set; }
    public long? UltimoTimeStep { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime? ConfirmadoEm { get; private set; }
    public DateTime? AtualizadoEm { get; private set; }

    private ContaMfa() { }

    public static Result<ContaMfa> Criar(Guid contaId, string secretCifrado, DateTime agora)
    {
        if (contaId == Guid.Empty)
            return Result.Failure<ContaMfa>(MfaErrors.ContaIdInvalido);

        if (string.IsNullOrWhiteSpace(secretCifrado))
            return Result.Failure<ContaMfa>(MfaErrors.SecretObrigatorio);

        return Result.Success(new ContaMfa
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            TotpSecretCifrado = secretCifrado,
            Habilitado = false,
            CriadoEm = agora
        });
    }

    public Result AtualizarSecretPendente(string secretCifrado, DateTime agora)
    {
        if (Habilitado)
            return Result.Failure(MfaErrors.JaConfirmado);

        if (string.IsNullOrWhiteSpace(secretCifrado))
            return Result.Failure(MfaErrors.SecretObrigatorio);

        TotpSecretCifrado = secretCifrado;
        ConfirmadoEm = null;
        UltimoTimeStep = null;
        AtualizadoEm = agora;
        return Result.Success();
    }

    public Result Confirmar(long timeStep, DateTime agora)
    {
        if (Habilitado)
            return Result.Failure(MfaErrors.JaConfirmado);

        Habilitado = true;
        ConfirmadoEm = agora;
        UltimoTimeStep = timeStep;
        AtualizadoEm = agora;
        return Result.Success();
    }

    public Result RegistrarUso(long timeStep, DateTime agora)
    {
        if (!Habilitado)
            return Result.Failure(MfaErrors.NaoHabilitado);

        if (UltimoTimeStep.HasValue && timeStep <= UltimoTimeStep.Value)
            return Result.Failure(MfaErrors.CodigoReutilizado);

        UltimoTimeStep = timeStep;
        AtualizadoEm = agora;
        return Result.Success();
    }

    public void Desabilitar(DateTime agora)
    {
        TotpSecretCifrado = null;
        Habilitado = false;
        ConfirmadoEm = null;
        UltimoTimeStep = null;
        AtualizadoEm = agora;
    }
}
