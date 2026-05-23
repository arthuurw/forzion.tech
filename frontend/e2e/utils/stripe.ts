/**
 * Stripe test mode card numbers. Documentacao oficial:
 * https://docs.stripe.com/testing#cards
 *
 * Usado em specs de checkout (Fase 10) — esses numeros so funcionam em test
 * mode (chave pk_test_*). Producao deve continuar bloqueando-os.
 */

export const STRIPE_TEST_CARDS = {
  success: "4242 4242 4242 4242",
  decline: "4000 0000 0000 0002",
  insufficient: "4000 0000 0000 9995",
  threeDSecure: "4000 0027 6000 3184",
  threeDSecureRequired: "4000 0000 0000 3220",
  expired: "4000 0000 0000 0069",
  incorrectCvc: "4000 0000 0000 0127",
} as const;

export const STRIPE_TEST_EXPIRY = "12 / 34"; // Qualquer data futura serve
export const STRIPE_TEST_CVC = "123";
export const STRIPE_TEST_ZIP = "01310-100"; // CEP brasileiro generico
