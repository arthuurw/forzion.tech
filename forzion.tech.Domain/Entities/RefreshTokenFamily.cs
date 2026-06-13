using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

/// <summary>
/// Aggregate root de uma sessão de login (um device). Agrupa a cadeia de refresh tokens
/// rotacionados; revogar a família invalida toda a cadeia de uma vez (reuse detection,
/// logout, troca de senha). AbsolutoExpiraEm = teto absoluto não-renovável (idle ≠ eterno).
/// </summary>
public class RefreshTokenFamily
{
    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public DateTime CriadaEm { get; private set; }
    public DateTime AbsolutoExpiraEm { get; private set; }
    public DateTime? RevogadaEm { get; private set; }
    public MotivoRevogacaoFamilia? MotivoRevogacao { get; private set; }
    public string? Rotulo { get; private set; }

    private RefreshTokenFamily() { }

    public static Result<RefreshTokenFamily> Criar(Guid contaId, DateTime absolutoExpiraEm, DateTime agora, string? rotulo = null)
    {
        if (contaId == Guid.Empty)
            return Result.Failure<RefreshTokenFamily>(RefreshErrors.ContaIdInvalido);
        if (absolutoExpiraEm <= agora)
            return Result.Failure<RefreshTokenFamily>(RefreshErrors.AbsolutoNaoFuturo);

        return Result.Success(new RefreshTokenFamily
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            CriadaEm = agora,
            AbsolutoExpiraEm = absolutoExpiraEm,
            Rotulo = rotulo,
        });
    }

    public Result Revogar(MotivoRevogacaoFamilia motivo, DateTime agora)
    {
        if (RevogadaEm.HasValue)
            return Result.Failure(RefreshErrors.FamiliaJaRevogada);

        RevogadaEm = agora;
        MotivoRevogacao = motivo;
        return Result.Success();
    }

    public bool EstaAtiva(DateTime agora) => !RevogadaEm.HasValue && agora < AbsolutoExpiraEm;
}
