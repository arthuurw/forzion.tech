namespace forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;

public record ProcessarWebhookStripeCommand(string Payload, string AssinaturaAlunoStripe);
