using forzion.tech.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.Services;

public sealed class ReembolsoArrependimentoService(
    IStripeService stripeService,
    ILogger<ReembolsoArrependimentoService> logger)
{
    private const int PrazoArrependimentoDias = 7;

    public async Task ReembolsarSeDentroDoPrazoAsync(
        DateTime agora,
        string? paymentIntentId,
        DateTime? dataPagamento,
        bool reverterTransferencia,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(paymentIntentId) || dataPagamento is not { } pago)
            return;

        if ((agora - pago).TotalDays > PrazoArrependimentoDias)
            return;

        try
        {
            await stripeService.CriarReembolsoAsync(paymentIntentId, reverterTransferencia, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Falha ao reembolsar pagamento {PaymentIntentId} no cancelamento de arrependimento (7 dias). Cancelamento prossegue; reembolso manual necessário.",
                paymentIntentId);
        }
    }
}
