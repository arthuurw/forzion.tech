using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

/// <summary>
/// Refresh token single-use de uma <see cref="RefreshTokenFamily"/>. O raw vive só no cookie;
/// o DB guarda apenas o SHA-256 (NR-1). Rotação: usar marca <see cref="UsadoEm"/> e aponta o
/// <see cref="SubstituidoPorId"/> (cadeia). Reuso de token já usado = sinal de roubo → o handler
/// revoga a família inteira.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid FamiliaId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CriadoEm { get; private set; }
    public DateTime ExpiraEm { get; private set; }
    public DateTime? UsadoEm { get; private set; }
    public Guid? SubstituidoPorId { get; private set; }

    private RefreshToken() { }

    public static Result<RefreshToken> Criar(Guid familiaId, string tokenHash, DateTime expiraEm, DateTime agora)
    {
        if (familiaId == Guid.Empty)
            return Result.Failure<RefreshToken>(RefreshErrors.FamiliaIdInvalido);
        if (string.IsNullOrWhiteSpace(tokenHash))
            return Result.Failure<RefreshToken>(RefreshErrors.TokenHashObrigatorio);
        if (expiraEm <= agora)
            return Result.Failure<RefreshToken>(RefreshErrors.ExpiracaoNaoFutura);

        return Result.Success(new RefreshToken
        {
            Id = Guid.NewGuid(),
            FamiliaId = familiaId,
            TokenHash = tokenHash,
            CriadoEm = agora,
            ExpiraEm = expiraEm,
        });
    }

    public Result MarcarUsado(DateTime agora, Guid sucessorId)
    {
        if (UsadoEm.HasValue)
            return Result.Failure(RefreshErrors.TokenJaUtilizado);
        if (sucessorId == Guid.Empty)
            return Result.Failure(RefreshErrors.SucessorInvalido);

        UsadoEm = agora;
        SubstituidoPorId = sucessorId;
        return Result.Success();
    }

    // Validade do TOKEN apenas (não-usado ∧ dentro do idle). A atividade da FAMÍLIA
    // (revogação + teto absoluto) é checada à parte via RefreshTokenFamily.EstaAtiva —
    // o token não referencia o estado da família.
    public bool EstaValido(DateTime agora) => !UsadoEm.HasValue && agora < ExpiraEm;
}
