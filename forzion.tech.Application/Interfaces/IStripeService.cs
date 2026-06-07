namespace forzion.tech.Application.Interfaces;

public interface IStripeService
{
    Task<string> CriarContaConnectAsync(string email, string nome, CancellationToken cancellationToken = default);
    Task<string> GerarLinkOnboardingAsync(string stripeAccountId, string urlRetorno, string urlCancelamento, CancellationToken cancellationToken = default);
    Task<PixPaymentResult> CriarPixPaymentIntentAsync(decimal valor, string stripeAccountId, Guid pagamentoId, decimal taxaPlataformaPercent, CancellationToken cancellationToken = default);
    Task<CartaoPaymentResult> CriarCartaoPaymentIntentAsync(decimal valor, string stripeAccountId, Guid pagamentoId, decimal taxaPlataformaPercent, CancellationToken cancellationToken = default);

    // Pagamento do plano do treinador: valor cheio para a conta da plataforma (sem Connect/fee split).
    Task<PixPaymentResult> CriarPixPlataformaPaymentIntentAsync(decimal valor, Guid pagamentoTreinadorId, CancellationToken cancellationToken = default);
    Task<CartaoPaymentResult> CriarCartaoPlataformaPaymentIntentAsync(decimal valor, Guid pagamentoTreinadorId, CancellationToken cancellationToken = default);

    Task<bool> ContaEstaAtivadaAsync(string stripeAccountId, CancellationToken cancellationToken = default);
    Task<bool> ValidarWebhookAsync(string payload, string assinaturaStripe);

    // reverterTransferencia=true (charge destino do aluno): reverte a transferência ao treinador
    // e a application fee — sem isso o refund deixa o dinheiro no treinador e pode falhar
    // balance_insufficient. false (charge direto na plataforma, ex. plano do treinador): refund simples.
    // Reembolso é sempre total (Amount não enviado).
    Task CriarReembolsoAsync(string paymentIntentId, bool reverterTransferencia, CancellationToken cancellationToken = default);

    // Anexa evidências automáticas a um chargeback (R9). Stripe exige disputeId, não chargeId
    // (Dispute.Update). Sem resposta no prazo (~7d) o chargeback é perdido + fee.
    Task EnviarEvidenciaDisputaAsync(string disputeId, DisputaEvidencia evidencias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista eventos Stripe criados a partir de <paramref name="desdeUtc"/>, filtrando
    /// pelos tipos relevantes para reconciliação de pagamentos e onboarding Connect.
    /// Resultado ordenado por <see cref="StripeEventSummary.Created"/> ASC (mais antigo primeiro)
    /// e capado para um teto interno de segurança (evita varreduras patológicas).
    /// </summary>
    Task<IReadOnlyList<StripeEventSummary>> ListarEventosDesdeAsync(DateTime desdeUtc, CancellationToken cancellationToken = default);
}

public record PixPaymentResult(
    string PaymentIntentId,
    string QrCode,
    string QrCodeUrl,
    DateTime Expiracao);

public record CartaoPaymentResult(
    string PaymentIntentId,
    string ClientSecret);

// Evidências coletáveis sem nova coluna (D9): email da conta, data de ativação (ServiceDate),
// última atividade/uso e data do último pagamento — usadas pra contestar o chargeback no Stripe.
public record DisputaEvidencia(
    string? EmailCliente,
    DateTime? DataAtivacao,
    DateTime? DataUltimaAtividade,
    DateTime? DataUltimoPagamento);

/// <summary>
/// Snapshot de um evento Stripe retornado por <c>Events.List</c>, no formato que o
/// reconciliador precisa para reprocessar via <see cref="forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe.ProcessarWebhookStripeHandler"/>.
/// <para>
/// <c>PayloadRaw</c> é o JSON serializado do evento — equivalente ao body que o webhook receberia
/// em entrega normal — e é parseado pelo mesmo <c>StripeWebhookParser</c>, garantindo paridade
/// de comportamento entre webhook e reconciliação.
/// </para>
/// </summary>
public sealed record StripeEventSummary(
    string EventId,
    string Type,
    string PayloadRaw,
    DateTime Created);
