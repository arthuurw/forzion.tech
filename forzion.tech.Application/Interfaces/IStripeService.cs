namespace forzion.tech.Application.Interfaces;

public interface IStripeService
{
    Task<string> CriarContaConnectAsync(string email, string nome, CancellationToken cancellationToken = default);
    Task<string> GerarLinkOnboardingAsync(string stripeAccountId, string urlRetorno, string urlCancelamento, CancellationToken cancellationToken = default);
    Task<PixPaymentResult> CriarPixPaymentIntentAsync(decimal valor, string stripeAccountId, Guid pagamentoId, decimal taxaPlataformaPercent, CancellationToken cancellationToken = default);
    Task<CartaoPaymentResult> CriarCartaoPaymentIntentAsync(decimal valor, string stripeAccountId, Guid pagamentoId, decimal taxaPlataformaPercent, CancellationToken cancellationToken = default);
    Task<bool> ContaEstaAtivadaAsync(string stripeAccountId, CancellationToken cancellationToken = default);
    Task<bool> ValidarWebhookAsync(string payload, string assinaturaStripe);
}

public record PixPaymentResult(
    string PaymentIntentId,
    string QrCode,
    string QrCodeUrl,
    DateTime Expiracao);

public record CartaoPaymentResult(
    string PaymentIntentId,
    string ClientSecret);
