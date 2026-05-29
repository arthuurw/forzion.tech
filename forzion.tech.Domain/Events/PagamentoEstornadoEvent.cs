namespace forzion.tech.Domain.Events;

/// <summary>
/// Disparado em <see cref="Entities.Pagamento.MarcarEstornado"/> quando webhook
/// Stripe <c>charge.refunded</c> entrega refund iniciado manualmente pelo treinador
/// no Stripe Dashboard.
///
/// Handler usa <c>AssinaturaAlunoId</c> → <c>IAssinaturaAlunoRepository</c> →
/// <c>IAlunoRepository</c> → fallback <c>Conta.Email</c>/<c>Aluno.Telefone</c>
/// pra notificar aluno (mesmo pattern de <see cref="PagamentoCriadoEvent"/>).
///
/// Decisão: refund NÃO cascateia em cancelamento de assinatura — treinador atua
/// manualmente via painel admin se quiser encerrar relação. Refund é operação
/// rara, automatizar cancel implícito mais arrisca do que ajuda.
/// </summary>
public sealed record PagamentoEstornadoEvent(
    Guid PagamentoId,
    Guid AssinaturaAlunoId,
    decimal Valor,
    DateTime OcorridoEm) : IDomainEvent;
