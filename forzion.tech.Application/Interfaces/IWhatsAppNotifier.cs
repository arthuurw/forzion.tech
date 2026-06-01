namespace forzion.tech.Application.Interfaces;

public interface IWhatsAppNotifier
{
    /// <summary>Indica se o provedor real está configurado (Meta) ou é no-op (Null).</summary>
    bool Habilitado { get; }

    /// <summary>
    /// Envia mensagem de texto livre (sessão). Só é entregue pela Meta DENTRO da janela
    /// de atendimento de 24h. Para notificações business-initiated use <see cref="SendTemplateAsync"/>.
    /// </summary>
    Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia mensagem de template aprovado (entrega fora da janela de 24h).
    /// </summary>
    Task SendTemplateAsync(string phoneNumber, WhatsAppTemplateMessage message, CancellationToken cancellationToken = default);
}
