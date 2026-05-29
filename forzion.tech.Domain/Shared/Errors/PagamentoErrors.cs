namespace forzion.tech.Domain.Shared.Errors;

public static class PagamentoErrors
{
    public static Error AssinaturaIdInvalido => new("pagamento.assinatura_id_invalido", "O identificador da assinatura é inválido.");
    public static Error ValorInvalido => new("pagamento.valor_invalido", "O valor do pagamento deve ser maior que zero.");
    public static Error PaymentIntentIdInvalido => new("pagamento.payment_intent_id_invalido", "O identificador do PaymentIntent é inválido.");
    public static Error QrCodeInvalido => new("pagamento.qr_code_invalido", "O QR code Pix é inválido.");
    public static Error ClientSecretInvalido => new("pagamento.client_secret_invalido", "O client secret do cartão é inválido.");
    public static Error ApenasPendentesPagos => new("pagamento.apenas_pendentes_pagos", "Apenas pagamentos pendentes podem ser marcados como pagos.");
    public static Error ApenasPendentesFalhou => new("pagamento.apenas_pendentes_falhou", "Apenas pagamentos pendentes podem ser marcados como falhou.");
    public static Error ApenasPendentesExpirados => new("pagamento.apenas_pendentes_expirados", "Apenas pagamentos pendentes podem ser marcados como expirados.");
    public static Error ApenasPagosEstornados => new("pagamento.apenas_pagos_estornados", "Apenas pagamentos pagos podem ser estornados.");
    public static Error ApenasPagosEmDisputa => new("pagamento.apenas_pagos_em_disputa", "Apenas pagamentos pagos podem ser marcados em disputa.");
}
