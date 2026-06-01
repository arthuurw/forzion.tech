namespace forzion.tech.Domain.Entities;

public class WhatsAppDeliveryLog
{
    public Guid Id { get; private set; }
    public string MetaMessageId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string RecipientPhone { get; private set; } = string.Empty;
    public DateTime OcorridoEm { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private WhatsAppDeliveryLog() { }

    public static WhatsAppDeliveryLog Criar(
        string metaMessageId,
        string eventType,
        string recipientPhone,
        DateTime ocorridoEm,
        string payload,
        DateTime agora)
    {
        return new WhatsAppDeliveryLog
        {
            Id = Guid.NewGuid(),
            MetaMessageId = metaMessageId,
            EventType = eventType,
            RecipientPhone = recipientPhone,
            OcorridoEm = ocorridoEm,
            Payload = payload,
            CreatedAt = agora
        };
    }
}
