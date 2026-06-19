namespace forzion.tech.Domain.Shared.Errors;

public static class PagamentoErrors
{
    public static Error AssinaturaIdInvalido => Error.Validation("pagamento.assinatura_id_invalido", "O identificador da assinatura é inválido.");
    public static Error ValorInvalido => Error.Validation("pagamento.valor_invalido", "O valor do pagamento deve ser maior que zero.");
    public static Error PaymentIntentIdInvalido => Error.Validation("pagamento.payment_intent_id_invalido", "O identificador do PaymentIntent é inválido.");
    public static Error QrCodeInvalido => Error.Validation("pagamento.qr_code_invalido", "O QR code Pix é inválido.");
    public static Error ClientSecretInvalido => Error.Validation("pagamento.client_secret_invalido", "O client secret do cartão é inválido.");
    public static Error ApenasPendentesPagos => Error.Conflict("pagamento.apenas_pendentes_pagos", "Apenas pagamentos pendentes podem ser marcados como pagos.");
    public static Error ApenasPendentesFalhou => Error.Conflict("pagamento.apenas_pendentes_falhou", "Apenas pagamentos pendentes podem ser marcados como falhou.");
    public static Error ApenasPendentesExpirados => Error.Conflict("pagamento.apenas_pendentes_expirados", "Apenas pagamentos pendentes podem ser marcados como expirados.");
    public static Error ApenasPagosEstornados => Error.Conflict("pagamento.apenas_pagos_estornados", "Apenas pagamentos pagos podem ser estornados.");
    public static Error ApenasPagosEmDisputa => Error.Conflict("pagamento.apenas_pagos_em_disputa", "Apenas pagamentos pagos podem ser marcados em disputa.");
}
