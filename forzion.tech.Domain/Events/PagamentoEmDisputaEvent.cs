namespace forzion.tech.Domain.Events;

/// <summary>
/// Disparado em <see cref="Entities.Pagamento.MarcarEmDisputa"/> quando webhook
/// Stripe <c>charge.dispute.created</c> entrega chargeback iniciado pelo cliente
/// (dispute do cartão junto ao banco).
///
/// Handler notifica treinador via e-mail urgente (precisa responder via
/// dashboard.stripe.com em 7-21 dias) e loga em <c>LogLevel.Critical</c> para
/// monitoring/alerting (Arthur acompanha por agregador de log).
///
/// Decisão: a assinatura é forçada para Inadimplente via
/// <see cref="Entities.AssinaturaAluno.MarcarInadimplentePorDisputa"/> — disputa é
/// sinal forte que o aluno não quer continuar OU foi fraude, então o acesso é
/// congelado imediatamente, sem esperar o threshold normal de tentativas falhas.
/// </summary>
public sealed record PagamentoEmDisputaEvent(
    Guid PagamentoId,
    Guid AssinaturaAlunoId,
    decimal Valor,
    string MotivoDisputa,
    DateTime OcorridoEm) : IDomainEvent;
