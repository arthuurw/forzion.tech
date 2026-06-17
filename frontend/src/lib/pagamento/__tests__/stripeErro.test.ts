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

  it("code desconhecido → usa error.message quando presente", () => {
    expect(mapStripeError({ code: "algo_novo", message: "msg do stripe em pt" }))
      .toBe("msg do stripe em pt");
  });

  it("desconhecido sem message → fallback pt-BR", () => {
    expect(mapStripeError({ code: "algo_novo" }))
      .toBe("Não foi possível processar o pagamento. Verifique os dados do cartão.");
  });
});
