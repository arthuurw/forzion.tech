import { describe, it, expect } from "vitest";
import { mapStripeError } from "../stripeErro";

describe("mapStripeError", () => {
  it("decline_code conhecido → cópia pt-BR curada", () => {
    expect(mapStripeError({ code: "card_declined", decline_code: "insufficient_funds" }))
      .toBe("Saldo ou limite insuficiente. Tente outro cartão.");
  });

  it("code conhecido sem decline_code → cópia pt-BR", () => {
    expect(mapStripeError({ code: "expired_card" }))
      .toBe("Cartão expirado. Use um cartão válido.");
  });

  it("code desconhecido → fallback pt-BR (não vaza mensagem inglesa do Stripe)", () => {
    expect(mapStripeError({ code: "card_velocity_exceeded" }))
      .toBe("Não foi possível processar o pagamento. Verifique os dados do cartão.");
  });

  it("sem code nem decline_code → fallback pt-BR", () => {
    expect(mapStripeError({}))
      .toBe("Não foi possível processar o pagamento. Verifique os dados do cartão.");
  });
});
