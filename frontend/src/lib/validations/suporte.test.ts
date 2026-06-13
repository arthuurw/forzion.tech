import { describe, it, expect } from "vitest";
import { suporteSchema, CATEGORIA_LABEL, CATEGORIAS_SUPORTE } from "@/lib/validations/suporte";

const valido = {
  categoria: "Duvida" as const,
  assunto: "Não consigo acessar",
  descricao: "Descrição longa o suficiente para passar.",
};

describe("suporteSchema", () => {
  it("aceita payload válido", () => {
    expect(suporteSchema.safeParse(valido).success).toBe(true);
  });

  it("rejeita categoria fora do enum", () => {
    expect(suporteSchema.safeParse({ ...valido, categoria: "Reclamacao" }).success).toBe(false);
  });

  it("rejeita assunto curto (< 3)", () => {
    expect(suporteSchema.safeParse({ ...valido, assunto: "ab" }).success).toBe(false);
  });

  it("rejeita assunto longo (> 120)", () => {
    expect(suporteSchema.safeParse({ ...valido, assunto: "a".repeat(121) }).success).toBe(false);
  });

  it("rejeita descrição curta (< 20)", () => {
    expect(suporteSchema.safeParse({ ...valido, descricao: "curta" }).success).toBe(false);
  });

  it("rejeita descrição longa (> 2000)", () => {
    expect(suporteSchema.safeParse({ ...valido, descricao: "a".repeat(2001) }).success).toBe(false);
  });

  it("trima antes de validar: espaços não contam pro mínimo", () => {
    expect(suporteSchema.safeParse({ ...valido, assunto: "  a  " }).success).toBe(false);
  });

  it("CATEGORIA_LABEL cobre todas as categorias", () => {
    CATEGORIAS_SUPORTE.forEach((c) => expect(CATEGORIA_LABEL[c]).toBeTruthy());
  });
});
