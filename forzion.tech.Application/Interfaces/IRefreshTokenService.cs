using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces;

public enum ResultadoRotacao
{
    Sucesso,
    Invalido,
    ReuseDetectado,
}

public record RefreshEmitido(string RefreshRaw, Guid FamiliaId);

/// <summary>
/// Resultado da rotação. Em <see cref="ResultadoRotacao.Sucesso"/> traz a <see cref="Conta"/>
/// (p/ reemitir o access) + o novo refresh raw. Em <see cref="ResultadoRotacao.ReuseDetectado"/>
/// a família já foi revogada (caller só precisa commitar + logar + 401).
/// </summary>
public record RotacaoResultado(ResultadoRotacao Resultado, Conta? Conta, Guid FamiliaId, string? RefreshRaw)
{
    public static RotacaoResultado Invalido() => new(ResultadoRotacao.Invalido, null, Guid.Empty, null);
    public static RotacaoResultado Reuse(Guid familiaId) => new(ResultadoRotacao.ReuseDetectado, null, familiaId, null);
    public static RotacaoResultado Sucesso(Conta conta, Guid familiaId, string refreshRaw) =>
        new(ResultadoRotacao.Sucesso, conta, familiaId, refreshRaw);
}

/// <summary>
/// Ciclo de vida do refresh token (rotação single-use + reuse detection + revogação de família).
/// Os métodos NÃO commitam — o handler chamador controla o <see cref="IUnitOfWork"/>.
/// </summary>
public interface IRefreshTokenService
{
    Task<RefreshEmitido> EmitirNovaFamiliaAsync(Conta conta, DateTime agora, string? rotulo, CancellationToken cancellationToken = default);
    Task<RotacaoResultado> RotacionarAsync(string refreshRaw, DateTime agora, CancellationToken cancellationToken = default);
    Task RevogarFamiliaAsync(Guid familiaId, MotivoRevogacaoFamilia motivo, DateTime agora, CancellationToken cancellationToken = default);
    Task RevogarTodasPorContaAsync(Guid contaId, MotivoRevogacaoFamilia motivo, DateTime agora, CancellationToken cancellationToken = default);
}
