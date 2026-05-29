namespace forzion.tech.Application.Interfaces;

/// <summary>Canais de notificação liberados pelo plano do treinador.</summary>
public sealed record CanaisNotificacao(bool Email, bool WhatsApp)
{
    public static readonly CanaisNotificacao Nenhum = new(false, false);
}

/// <summary>
/// Resolve, a partir do tier do plano do treinador, quais canais de notificação
/// (e-mail/WhatsApp) estão liberados. Free/Basic/sem-plano = só plataforma.
/// </summary>
public interface IPlanoNotificationPolicy
{
    Task<CanaisNotificacao> ResolverPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default);
    Task<CanaisNotificacao> ResolverPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
}
