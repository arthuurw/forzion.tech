namespace forzion.tech.Application.UseCases.Pagamentos.ReconciliarPagamentosStripe;

/// <summary>
/// Resultado agregado da reconciliação. <c>TotalEventos</c> = todos eventos retornados pela
/// Stripe Events.List dentro da janela; <c>Replayed</c> = mudaram estado;
/// <c>JaConsistentes</c> = no-op (idempotência ou alvo não localizado);
/// <c>Erros</c> = exceções capturadas durante replay (logadas, varredura prossegue).
/// </summary>
public sealed record ReconciliarPagamentosStripeResponse(
    int TotalEventos,
    int Replayed,
    int JaConsistentes,
    int Erros,
    DateTime DesdeUtc);
