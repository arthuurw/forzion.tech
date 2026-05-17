namespace forzion.tech.Infrastructure.Services;

public class StripeSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public decimal TaxaPlataformaPercent { get; set; } = 5m;
    public string UrlBase { get; set; } = string.Empty;
}
