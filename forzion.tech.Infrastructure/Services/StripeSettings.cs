namespace forzion.tech.Infrastructure.Services;

public class StripeSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public decimal TaxaPlataformaPercent { get; set; } = 5m;
    public string UrlBase { get; set; } = string.Empty;

    // SEC-03: modo esperado dos eventos de webhook. null = sem enforcement. Default derivado do
    // ambiente em DI (Production⇒true; demais⇒false, p/ não bloquear test-mode em Homolog público).
    public bool? ExpectLivemode { get; set; }

    public int TimeoutSegundos { get; set; } = 30;
    public int MaxNetworkRetries { get; set; } = 2;

    // Teto de eventos por run da reconciliação (memória bounded); atingiu ⇒ trunca + sinaliza backlog.
    public int MaxEventosReconciliacaoPorRun { get; set; } = 1000;
}
