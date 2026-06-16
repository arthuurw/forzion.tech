namespace forzion.tech.Application.Settings;

public class DeliveryLogSettings
{
    public const string DevDefaultKey = "dev-only-delivery-log-hash-key";

    public string RecipientHashKey { get; set; } = string.Empty;
}
