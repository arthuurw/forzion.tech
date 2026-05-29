namespace forzion.tech.Domain.Enums;

public enum PagamentoStatus
{
    Pendente,
    Pago,
    Expirado,
    Falhou,
    Estornado,
    /// <summary>
    /// Cliente abriu disputa (chargeback) com o banco/Stripe sobre um pagamento já
    /// capturado. Só faz sentido transicionar a partir de <see cref="Pago"/> — disputa
    /// sobre Pendente/Falhou/Expirado/Estornado é incoerente. Stripe envia
    /// <c>charge.dispute.created</c> quando isso acontece e dá 7-21 dias pro treinador
    /// responder via dashboard.stripe.com (não temos UI própria para isso).
    /// </summary>
    EmDisputa
}
