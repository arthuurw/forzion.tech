namespace forzion.tech.Domain.Enums;

/// <summary>
/// Regras de canal de notificação por tier de plano da plataforma.
/// Ordenação implícita do enum: Free(0) &lt; Basic(1) &lt; Pro(2) &lt; ProPlus(3) &lt; Elite(4).
/// E-mail libera a partir de Pro; WhatsApp a partir de ProPlus. Free/Basic = só plataforma.
/// </summary>
public static class TierPlanoExtensions
{
    public static bool PermiteEmail(this TierPlano tier) => tier >= TierPlano.Pro;

    public static bool PermiteWhatsApp(this TierPlano tier) => tier >= TierPlano.ProPlus;
}
