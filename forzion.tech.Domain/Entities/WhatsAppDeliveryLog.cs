namespace forzion.tech.Domain.Entities;

public class WhatsAppDeliveryLog
{
    public Guid Id { get; private set; }
    public string MetaMessageId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string RecipientPhoneHash { get; private set; } = string.Empty;
    public DateTime OcorridoEm { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private WhatsAppDeliveryLog() { }

    public static WhatsAppDeliveryLog Criar(
        string metaMessageId,
        string eventType,
        string recipientPhoneHash,
        DateTime ocorridoEm,
        DateTime agora)
    {
        return new WhatsAppDeliveryLog
        {
            Id = Guid.NewGuid(),
            MetaMessageId = metaMessageId,
            EventType = eventType,
            RecipientPhoneHash = recipientPhoneHash,
            OcorridoEm = ocorridoEm,
            CreatedAt = agora
        };
    }
}
