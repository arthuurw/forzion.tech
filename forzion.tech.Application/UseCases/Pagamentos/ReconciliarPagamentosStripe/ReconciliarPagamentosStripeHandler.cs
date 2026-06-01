using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Shared;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Pagamentos.ReconciliarPagamentosStripe;

/// <summary>
/// Safety-net contra webhooks Stripe perdidos: poll <c>Events.List</c> dos últimos
/// <c>N</c> dias e reprocessa cada evento via <see cref="ProcessarWebhookStripeHandler.ProcessarEventoAsync"/>.
/// <para>
/// Idempotência é garantida pelos próprios handlers (checks de <c>Status != Pendente</c>
/// e <c>OnboardingCompleto</c>) — replays seguros mesmo quando o webhook chegou e o evento
/// também foi pego pelo polling.
/// </para>
/// <para>
/// Assinatura Stripe NÃO é validada aqui — eventos vêm direto da API autenticados pela
/// nossa secret key, então não há vetor de spoofing como existe no endpoint público.
/// </para>
/// </summary>
public class ReconciliarPagamentosStripeHandler(
    IStripeService stripeService,
    ProcessarWebhookStripeHandler webhookHandler,
    TimeProvider timeProvider,
    ILogger<ReconciliarPagamentosStripeHandler> logger)
{
    private static readonly TimeSpan JanelaPadrao = TimeSpan.FromDays(7);

    public virtual async Task<Result<ReconciliarPagamentosStripeResponse>> HandleAsync(
        ReconciliarPagamentosStripeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var desde = command.DesdeUtc ?? timeProvider.GetUtcNow().UtcDateTime.Subtract(JanelaPadrao);

        logger.LogInformation("Iniciando reconciliação Stripe a partir de {DesdeUtc:o}.", desde);

        var eventos = await stripeService.ListarEventosDesdeAsync(desde, cancellationToken).ConfigureAwait(false);

        var replayed = 0;
        var jaConsistentes = 0;
        var erros = 0;

        foreach (var evt in eventos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var parsed = StripeWebhookParser.Parse(evt.PayloadRaw);
                var resultado = await webhookHandler.ProcessarEventoAsync(parsed, cancellationToken).ConfigureAwait(false);

                if (resultado == ProcessarEventoResultado.Aplicado)
                {
                    replayed++;
                    logger.LogInformation("Reconciliação aplicou evento {EventId} ({EventType}).", evt.EventId, evt.Type);
                }
                else
                {
                    // JaConsistente (idempotência/alvo ausente/cross-account rejeitado) e
                    // Ignorado (tipo fora do escopo) somam no mesmo bucket — não há ação útil.
                    jaConsistentes++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // varredura precisa absorver falha de um evento isolado p/ não abortar o batch
            catch (Exception ex)
#pragma warning restore CA1031
            {
                erros++;
                logger.LogError(ex, "Falha ao reprocessar evento Stripe {EventId} ({EventType}).", evt.EventId, evt.Type);
            }
        }

        logger.LogInformation(
            "Reconciliação concluída: total={Total} replayed={Replayed} jaConsistentes={JaConsistentes} erros={Erros}.",
            eventos.Count, replayed, jaConsistentes, erros);

        return Result.Success(new ReconciliarPagamentosStripeResponse(
            TotalEventos: eventos.Count,
            Replayed: replayed,
            JaConsistentes: jaConsistentes,
            Erros: erros,
            DesdeUtc: desde));
    }
}
