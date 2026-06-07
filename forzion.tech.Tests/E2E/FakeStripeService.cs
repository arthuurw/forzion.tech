using forzion.tech.Application.Interfaces;

namespace forzion.tech.Tests.E2E;

// Stub determinístico do Stripe para os testes E2E — não faz chamada externa.
// Retorna ids/links fabricados e sempre considera conta ativada e webhook válido.
public sealed class FakeStripeService : IStripeService
{
    public Task<string> CriarContaConnectAsync(string email, string nome, CancellationToken cancellationToken = default) =>
        Task.FromResult($"acct_fake_{Guid.NewGuid():N}");

    public Task<string> GerarLinkOnboardingAsync(string stripeAccountId, string urlRetorno, string urlCancelamento, CancellationToken cancellationToken = default) =>
        Task.FromResult("https://connect.fake/onboarding");

    public Task<PixPaymentResult> CriarPixPaymentIntentAsync(decimal valor, string stripeAccountId, Guid pagamentoId, decimal taxaPlataformaPercent, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PixPaymentResult(
            PaymentIntentId: $"pi_fake_{pagamentoId:N}",
            QrCode: "00020126fake-pix-qr-code",
            QrCodeUrl: "https://fake.stripe/qr.png",
            Expiracao: DateTime.UtcNow.AddMinutes(30)));

    public Task<CartaoPaymentResult> CriarCartaoPaymentIntentAsync(decimal valor, string stripeAccountId, Guid pagamentoId, decimal taxaPlataformaPercent, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CartaoPaymentResult(
            PaymentIntentId: $"pi_fake_{pagamentoId:N}",
            ClientSecret: $"pi_fake_{pagamentoId:N}_secret_fake"));

    public Task<PixPaymentResult> CriarPixPlataformaPaymentIntentAsync(decimal valor, Guid pagamentoTreinadorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PixPaymentResult(
            PaymentIntentId: $"pi_fake_treinador_{pagamentoTreinadorId:N}",
            QrCode: "00020126fake-pix-qr-code",
            QrCodeUrl: "https://fake.stripe/qr.png",
            Expiracao: DateTime.UtcNow.AddMinutes(30)));

    public Task<CartaoPaymentResult> CriarCartaoPlataformaPaymentIntentAsync(decimal valor, Guid pagamentoTreinadorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CartaoPaymentResult(
            PaymentIntentId: $"pi_fake_treinador_{pagamentoTreinadorId:N}",
            ClientSecret: $"pi_fake_treinador_{pagamentoTreinadorId:N}_secret_fake"));

    public Task<bool> ContaEstaAtivadaAsync(string stripeAccountId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<bool> ValidarWebhookAsync(string payload, string assinaturaStripe) =>
        Task.FromResult(true);

    public Task CriarReembolsoAsync(string paymentIntentId, bool reverterTransferencia, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task EnviarEvidenciaDisputaAsync(string disputeId, DisputaEvidencia evidencias, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<StripeEventSummary>> ListarEventosDesdeAsync(DateTime desdeUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StripeEventSummary>>(Array.Empty<StripeEventSummary>());
}
