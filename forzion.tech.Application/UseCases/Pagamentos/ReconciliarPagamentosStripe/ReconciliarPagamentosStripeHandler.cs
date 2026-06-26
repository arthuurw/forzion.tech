using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Pagamentos.ReconciliarPagamentosStripe;

/// <summary>
/// Safety-net contra webhooks Stripe perdidos: poll <c>Events.List</c> a partir de um cursor
/// persistido (high-water-mark) e reprocessa cada evento via
/// <see cref="ProcessarWebhookStripeHandler.ProcessarEventoAsync"/>.
/// <para>
/// Idempotência é garantida pelos próprios handlers (checks de <c>Status != Pendente</c>
/// e <c>OnboardingCompleto</c>) — replays seguros mesmo quando o webhook chegou e o evento
/// também foi pego pelo polling. Assinatura NÃO é validada: eventos vêm autenticados pela secret key.
/// </para>
/// <para>
/// O cursor avança incrementalmente (<see cref="ReconciliacaoStripeEstado.AvancarCursor"/>,
/// monotônico) conforme os eventos são processados, então um crash no meio do catch-up não
/// re-varre o já-processado. Truncamento (cap de batch) sinaliza backlog restante sem perda:
/// o cursor só passa do último evento processado.
/// </para>
/// </summary>
public class ReconciliarPagamentosStripeHandler(
    IStripeService stripeService,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ReconciliarPagamentosStripeHandler> logger)
{
    private static readonly TimeSpan JanelaMaxInicial = TimeSpan.FromDays(7);
    private const int LotePersistenciaCursor = 100;

    public virtual async Task<Result<ReconciliarPagamentosStripeResponse>> HandleAsync(
        ReconciliarPagamentosStripeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var janelaMax = agora.Subtract(JanelaMaxInicial);

        using var cursorScope = scopeFactory.CreateScope();
        var cursorRepo = cursorScope.ServiceProvider.GetRequiredService<IReconciliacaoStripeEstadoRepository>();
        var cursorUow = cursorScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var estado = await cursorRepo.ObterAsync(cancellationToken).ConfigureAwait(false);
        var cursorUtc = estado?.UltimoEventoReconciliadoUtc;

        var desde = command.DesdeUtc
            ?? (cursorUtc is { } c && c > janelaMax ? c : janelaMax);

        logger.LogInformation("Iniciando reconciliação Stripe a partir de {DesdeUtc:o}.", desde);

        var lote = await stripeService.ListarEventosDesdeAsync(desde, cancellationToken).ConfigureAwait(false);

        var replayed = 0;
        var jaConsistentes = 0;
        var erros = 0;
        var desdeUltimaPersistencia = 0;

        async Task PersistirCursorAsync(DateTime ate)
        {
            estado ??= ReconciliacaoStripeEstado.Criar(ate, agora);
            estado.AvancarCursor(ate, agora);
            await cursorRepo.SalvarAsync(estado, cancellationToken).ConfigureAwait(false);
            await cursorUow.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var evt in lote.Eventos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var parsed = StripeWebhookParser.Parse(evt.PayloadRaw);
                using var scope = scopeFactory.CreateScope();
                var webhookHandler = scope.ServiceProvider.GetRequiredService<ProcessarWebhookStripeHandler>();
                var resultado = await webhookHandler.ProcessarEventoAsync(parsed, cancellationToken).ConfigureAwait(false);

                if (resultado == ProcessarEventoResultado.Aplicado)
                {
                    replayed++;
                    logger.LogInformation("Reconciliação aplicou evento {EventId} ({EventType}).", evt.EventId, evt.Type);
                }
                else
                {
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

            if (++desdeUltimaPersistencia >= LotePersistenciaCursor)
            {
                await PersistirCursorAsync(evt.Created).ConfigureAwait(false);
                desdeUltimaPersistencia = 0;
            }
        }

        if (lote.Eventos.Count > 0)
            await PersistirCursorAsync(lote.Eventos[^1].Created).ConfigureAwait(false);

        logger.LogInformation(
            "Reconciliação concluída: total={Total} replayed={Replayed} jaConsistentes={JaConsistentes} erros={Erros} truncado={Truncado}.",
            lote.Eventos.Count, replayed, jaConsistentes, erros, lote.Truncado);

        return Result.Success(new ReconciliarPagamentosStripeResponse(
            TotalEventos: lote.Eventos.Count,
            Replayed: replayed,
            JaConsistentes: jaConsistentes,
            Erros: erros,
            DesdeUtc: desde,
            Truncado: lote.Truncado));
    }
}
