using System.Text.Json;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Outbox;

namespace forzion.tech.Infrastructure.Outbox.Handlers;

public sealed class EvidenciaDisputaEfeitoHandler(IStripeService stripeService) : IOutboxEfeitoHandler
{
    public string Tipo => "fx:evidencia_disputa";

    public async Task ExecutarAsync(string payload, CancellationToken cancellationToken = default)
    {
        var data = JsonSerializer.Deserialize<EvidenciaDisputaPayload>(payload)
            ?? throw new InvalidOperationException($"Payload inválido para {Tipo}: {payload}");

        // Dispute.Update é overwrite no Stripe — idempotente por natureza; retry seguro.
        await stripeService.EnviarEvidenciaDisputaAsync(
            data.DisputeId,
            new DisputaEvidencia(data.Email, data.DataAtivacao, null, data.DataPagamento),
            cancellationToken).ConfigureAwait(false);
    }
}
