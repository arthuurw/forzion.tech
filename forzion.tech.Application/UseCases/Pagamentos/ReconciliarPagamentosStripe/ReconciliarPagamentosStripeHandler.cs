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
/// O cursor avança (<see cref="ReconciliacaoStripeEstado.AvancarCursor"/>, monotônico) só até o
/// último evento processado com sucesso em cadeia contígua desde o início do lote (já ASC). Ao
/// primeiro erro de reprocessamento, o cursor CONGELA nesse ponto pelo resto do run — eventos
/// posteriores no mesmo lote que processem com sucesso NÃO avançam o cursor. Runs seguintes
/// re-varrem a partir do ponto congelado; como eventos já aplicados retornam JaConsistente
/// (idempotente), o re-scan é seguro.
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

    // account.updated de conta conectada NÃO volta no Events.List da plataforma (vem por Connect
    // webhook) — por isso a confirmação de onboarding é por poll Account.GetAsync, não por evento.
    // Cap: 1 GET Stripe por conta pendente, limita rate-limit.
    private const int MaxContasOnboardingPorRun = 200;

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
        DateTime? ultimoSucessoContiguoCreated = null;
        var cadeiaContiguaIntacta = true;

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

                if (cadeiaContiguaIntacta)
                    ultimoSucessoContiguoCreated = evt.Created;
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
                // Congela o avanço do cursor a partir daqui: eventos posteriores no lote não
                // são "contíguos" a partir do início do run, então não podem avançar o cursor
                // sem arriscar pular o evento que falhou (sem dead-letter, ele nunca mais seria varrido).
                cadeiaContiguaIntacta = false;
                logger.LogError(ex, "Falha ao reprocessar evento Stripe {EventId} ({EventType}).", evt.EventId, evt.Type);
            }

            if (++desdeUltimaPersistencia >= LotePersistenciaCursor)
            {
                if (ultimoSucessoContiguoCreated is { } ultimoParcial)
                    await PersistirCursorAsync(ultimoParcial).ConfigureAwait(false);
                desdeUltimaPersistencia = 0;
            }
        }

        if (ultimoSucessoContiguoCreated is { } ultimoSucesso)
            await PersistirCursorAsync(ultimoSucesso).ConfigureAwait(false);

        var onboardingConfirmados = await ReconciliarOnboardingConnectAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Reconciliação concluída: total={Total} replayed={Replayed} jaConsistentes={JaConsistentes} erros={Erros} truncado={Truncado} onboardingConfirmados={Onboarding}.",
            lote.Eventos.Count, replayed, jaConsistentes, erros, lote.Truncado, onboardingConfirmados);

        return Result.Success(new ReconciliarPagamentosStripeResponse(
            TotalEventos: lote.Eventos.Count,
            Replayed: replayed,
            JaConsistentes: jaConsistentes,
            Erros: erros,
            DesdeUtc: desde,
            Truncado: lote.Truncado,
            OnboardingConfirmados: onboardingConfirmados));
    }

    private async Task<int> ReconciliarOnboardingConnectAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var contaRepo = scope.ServiceProvider.GetRequiredService<IContaRecebimentoRepository>();
        var pendentes = await contaRepo
            .ListarConfiguradasPendentesOnboardingAsync(MaxContasOnboardingPorRun, cancellationToken)
            .ConfigureAwait(false);

        var confirmados = 0;

        foreach (var conta in pendentes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var ativada = await stripeService
                    .ContaEstaAtivadaAsync(conta.StripeConnectAccountId!, cancellationToken)
                    .ConfigureAwait(false);
                if (!ativada) continue;

                var evento = new StripeWebhookEvento("account.updated", null, conta.StripeConnectAccountId, ChargesEnabled: true);
                using var evtScope = scopeFactory.CreateScope();
                var webhookHandler = evtScope.ServiceProvider.GetRequiredService<ProcessarWebhookStripeHandler>();
                var resultado = await webhookHandler.ProcessarEventoAsync(evento, cancellationToken).ConfigureAwait(false);

                if (resultado == ProcessarEventoResultado.Aplicado)
                {
                    confirmados++;
                    logger.LogInformation("Onboarding Connect confirmado via reconciliação para treinador {TreinadorId}.", conta.TreinadorId);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // falha de uma conta isolada não aborta o restante da varredura
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Falha ao reconciliar onboarding da conta Connect {AccountId}.", conta.StripeConnectAccountId);
            }
        }

        return confirmados;
    }
}
