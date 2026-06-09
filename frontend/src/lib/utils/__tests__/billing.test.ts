import { describe, it, expect } from "vitest";
import { proximaCobranca } from "../billing";

describe("proximaCobranca", () => {
  it("não faz overflow em meses curtos (31/jan → fevereiro)", () => {
    expect(proximaCobranca(new Date(2025, 0, 31))).toBe("28/02/2025");
    expect(proximaCobranca(new Date(2024, 0, 31))).toBe("29/02/2024");
  });

  it("preserva o dia quando o mês alvo o comporta", () => {
    expect(proximaCobranca(new Date(2025, 4, 15))).toBe("15/06/2025");
  });

  it("avança para janeiro do ano seguinte a partir de dezembro", () => {
    expect(proximaCobranca(new Date(2025, 11, 10))).toBe("10/01/2026");
  });
});
