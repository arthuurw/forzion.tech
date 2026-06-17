using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class TrustedDevice
{
    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiraEm { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime? UltimoUsoEm { get; private set; }
    public string? Rotulo { get; private set; }
    public DateTime? RevogadoEm { get; private set; }

    private TrustedDevice() { }

    public static Result<TrustedDevice> Criar(Guid contaId, string tokenHash, DateTime expiraEm, DateTime agora, string? rotulo = null)
    {
        if (contaId == Guid.Empty)
            return Result.Failure<TrustedDevice>(MfaErrors.ContaIdInvalido);

        if (string.IsNullOrWhiteSpace(tokenHash))
            return Result.Failure<TrustedDevice>(MfaErrors.TokenHashObrigatorio);

        if (expiraEm <= agora)
            return Result.Failure<TrustedDevice>(MfaErrors.ExpiracaoNaoFutura);

        return Result.Success(new TrustedDevice
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            TokenHash = tokenHash,
            ExpiraEm = expiraEm,
            CriadoEm = agora,
            Rotulo = string.IsNullOrWhiteSpace(rotulo) ? null : rotulo.Trim()
        });
    }

    public bool EstaAtivo(DateTime agora) => !RevogadoEm.HasValue && agora < ExpiraEm;

    public void RegistrarUso(DateTime agora) => UltimoUsoEm = agora;

    public Result Revogar(DateTime agora)
    {
        if (RevogadoEm.HasValue)
            return Result.Failure(MfaErrors.DispositivoJaRevogado);

        RevogadoEm = agora;
        return Result.Success();
    }
}
