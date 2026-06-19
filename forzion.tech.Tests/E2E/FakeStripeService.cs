using System.Collections.Concurrent;
using forzion.tech.Application.Interfaces;

namespace forzion.tech.Tests.E2E;

// Stub determinístico do Stripe para os testes E2E — não faz chamada externa.
// Retorna ids/links fabricados e sempre considera conta ativada e webhook válido.
public sealed class FakeStripeService : IStripeService
{
    private readonly ConcurrentDictionary<string, byte> _idemKeys = new();

    public int PaymentIntentsCriados => _idemKeys.Count;
    public Task<string> CriarContaConnectAsync(string email, string nome, CancellationToken cancellationToken = default) =>
        Task.FromResult($"acct_fake_{Guid.NewGuid():N}");

    public Task<string> GerarLinkOnboardingAsync(string stripeAccountId, string urlRetorno, string urlCancelamento, CancellationToken cancellationToken = default) =>
        Task.FromResult("https://connect.fake/onboarding");

    private string PiId(string idempotencyKey)
    {
        _idemKeys.TryAdd(idempotencyKey, 0);
        return $"pi_fake_{idempotencyKey.Replace(':', '_')}";
    }

    public Task<PixPaymentResult> CriarPixPaymentIntentAsync(decimal valor, string stripeAccountId, decimal taxaPlataformaPercent, string idempotencyKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PixPaymentResult(
            PaymentIntentId: PiId(idempotencyKey),
            QrCode: "00020126fake-pix-qr-code",
            QrCodeUrl: "https://fake.stripe/qr.png",
            Expiracao: DateTime.UtcNow.AddMinutes(30)));

    public Task<CartaoPaymentResult> CriarCartaoPaymentIntentAsync(decimal valor, string stripeAccountId, decimal taxaPlataformaPercent, string idempotencyKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CartaoPaymentResult(
            PaymentIntentId: PiId(idempotencyKey),
            ClientSecret: $"{PiId(idempotencyKey)}_secret_fake"));

    public Task<PixPaymentResult> CriarPixPlataformaPaymentIntentAsync(decimal valor, string idempotencyKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PixPaymentResult(
            PaymentIntentId: PiId(idempotencyKey),
            QrCode: "00020126fake-pix-qr-code",
            QrCodeUrl: "https://fake.stripe/qr.png",
            Expiracao: DateTime.UtcNow.AddMinutes(30)));

    public Task<CartaoPaymentResult> CriarCartaoPlataformaPaymentIntentAsync(decimal valor, string idempotencyKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CartaoPaymentResult(
            PaymentIntentId: PiId(idempotencyKey),
            ClientSecret: $"{PiId(idempotencyKey)}_secret_fake"));

    public Task<bool> ContaEstaAtivadaAsync(string stripeAccountId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    // Echo do payload como evento verificado (E2E sem assinatura real).
    public Task<string?> ValidarWebhookAsync(string payload, string assinaturaStripe) =>
        Task.FromResult<string?>(payload);

    public Task CriarReembolsoAsync(Guid pagamentoId, string paymentIntentId, bool reverterTransferencia, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<CancelarPaymentIntentResultado> CancelarPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(CancelarPaymentIntentResultado.Cancelado);

    public Task EnviarEvidenciaDisputaAsync(string disputeId, DisputaEvidencia evidencias, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<StripeEventSummary>> ListarEventosDesdeAsync(DateTime desdeUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StripeEventSummary>>(Array.Empty<StripeEventSummary>());
}
