namespace forzion.tech.Domain.Shared.Errors;

public static class PagamentoTreinadorErrors
{
    public static Error TreinadorIdInvalido => new("pagamento_treinador.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error AssinaturaIdInvalido => new("pagamento_treinador.assinatura_id_invalido", "O identificador da assinatura é inválido.");
    public static Error ValorInvalido => new("pagamento_treinador.valor_invalido", "O valor do pagamento deve ser maior que zero.");
    public static Error PaymentIntentIdInvalido => new("pagamento_treinador.payment_intent_id_invalido", "O identificador do PaymentIntent é inválido.");
    public static Error QrCodeInvalido => new("pagamento_treinador.qr_code_invalido", "O QR Code do Pix é inválido.");
    public static Error ClientSecretInvalido => new("pagamento_treinador.client_secret_invalido", "O client secret é inválido.");
    public static Error ApenasPendentesPagos => new("pagamento_treinador.apenas_pendentes_pagos", "Apenas pagamentos pendentes podem ser marcados como pagos.");
    public static Error ApenasPendentesFalhou => new("pagamento_treinador.apenas_pendentes_falhou", "Apenas pagamentos pendentes podem ser marcados como falhos.");
    public static Error ApenasPendentesExpirados => new("pagamento_treinador.apenas_pendentes_expirados", "Apenas pagamentos pendentes podem ser marcados como expirados.");
    public static Error ApenasPagosEstornados => new("pagamento_treinador.apenas_pagos_estornados", "Apenas pagamentos pagos podem ser marcados como estornados.");
    public static Error ApenasPagosEmDisputa => new("pagamento_treinador.apenas_pagos_em_disputa", "Apenas pagamentos pagos podem ser marcados como em disputa.");
}
