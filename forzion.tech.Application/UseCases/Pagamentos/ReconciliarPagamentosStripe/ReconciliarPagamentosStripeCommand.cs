namespace forzion.tech.Application.UseCases.Pagamentos.ReconciliarPagamentosStripe;

/// <summary>
/// Comando para varrer eventos Stripe (via <c>Events.List</c>) criados a partir de
/// <paramref name="DesdeUtc"/> e reprocessar cada um contra os mesmos handlers do webhook live.
/// Tomado por endpoint interno; default = últimos 7 dias quando <c>null</c>.
/// </summary>
public sealed record ReconciliarPagamentosStripeCommand(DateTime? DesdeUtc = null);
