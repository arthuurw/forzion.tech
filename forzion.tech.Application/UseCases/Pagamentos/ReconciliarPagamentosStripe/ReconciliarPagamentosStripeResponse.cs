namespace forzion.tech.Application.UseCases.Pagamentos.ReconciliarPagamentosStripe;

/// <summary>
/// Resultado agregado da reconciliação. <c>TotalEventos</c> = eventos retornados pela
/// Stripe Events.List dentro do batch; <c>Replayed</c> = mudaram estado;
/// <c>JaConsistentes</c> = no-op (idempotência ou alvo não localizado);
/// <c>Erros</c> = exceções capturadas durante replay (logadas, varredura prossegue);
/// <c>Truncado</c> = cap de batch atingido, há backlog restante (cron deve re-disparar).
/// </summary>
public sealed record ReconciliarPagamentosStripeResponse(
    int TotalEventos,
    int Replayed,
    int JaConsistentes,
    int Erros,
    DateTime DesdeUtc,
    bool Truncado);
