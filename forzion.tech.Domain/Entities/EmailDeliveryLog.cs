namespace forzion.tech.Domain.Entities;

public class EmailDeliveryLog
{
    public Guid Id { get; private set; }
    public string ResendMessageId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string RecipientEmailHash { get; private set; } = string.Empty;
    public DateTime OcorridoEm { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private EmailDeliveryLog() { }

    public static EmailDeliveryLog Criar(
        string resendMessageId,
        string eventType,
        string recipientEmailHash,
        DateTime ocorridoEm,
        DateTime agora)
    {
        return new EmailDeliveryLog
        {
            Id = Guid.NewGuid(),
            ResendMessageId = resendMessageId,
            EventType = eventType,
            RecipientEmailHash = recipientEmailHash,
            OcorridoEm = ocorridoEm,
            CreatedAt = agora
        };
    }
}
