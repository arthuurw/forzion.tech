import type { StripeError } from "@stripe/stripe-js";

const FALLBACK =
  "Não foi possível processar o pagamento. Verifique os dados do cartão.";

const MENSAGENS: Record<string, string> = {
  card_declined: "Cartão recusado. Tente outro cartão ou fale com seu banco.",
  insufficient_funds: "Saldo ou limite insuficiente. Tente outro cartão.",
  expired_card: "Cartão expirado. Use um cartão válido.",
  incorrect_cvc: "Código de segurança (CVC) incorreto. Verifique e tente de novo.",
  incorrect_number: "Número do cartão incorreto. Verifique e tente de novo.",
  processing_error:
    "Erro ao processar o pagamento. Aguarde um instante e tente novamente.",
};

export function mapStripeError(error: Pick<StripeError, "code" | "decline_code">): string {
  const { code, decline_code } = error;
  return (
    (decline_code && MENSAGENS[decline_code]) ||
    (code && MENSAGENS[code]) ||
    FALLBACK
  );
}
