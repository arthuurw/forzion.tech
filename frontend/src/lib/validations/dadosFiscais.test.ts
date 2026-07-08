import { describe, it, expect } from "vitest";
import { cpfValido, cnpjValido, dadosFiscaisSchema } from "@/lib/validations/dadosFiscais";

const enderecoBase = {
  razaoSocial: "Maria Treinadora",
  logradouro: "Rua das Flores",
  numero: "100",
  bairro: "Centro",
  codigoMunicipioIbge: "3550308",
  uf: "SP",
  cep: "01001-000",
};

describe("cpfValido", () => {
  it("aceita CPF com DV correto", () => {
    expect(cpfValido("11144477735")).toBe(true);
  });
  it("rejeita CPF com DV incorreto", () => {
    expect(cpfValido("11144477736")).toBe(false);
  });
  it("rejeita CPF com todos os dígitos iguais", () => {
    expect(cpfValido("11111111111")).toBe(false);
  });
  it("rejeita CPF com comprimento diferente de 11", () => {
    expect(cpfValido("111444777")).toBe(false);
  });
});

describe("cnpjValido", () => {
  it("aceita CNPJ com DV correto", () => {
    expect(cnpjValido("11222333000181")).toBe(true);
  });
  it("rejeita CNPJ com DV incorreto", () => {
    expect(cnpjValido("11222333000180")).toBe(false);
  });
  it("rejeita CNPJ com todos os dígitos iguais", () => {
    expect(cnpjValido("11111111111111")).toBe(false);
  });
  it("rejeita CNPJ com comprimento diferente de 14", () => {
    expect(cnpjValido("1122233300018")).toBe(false);
  });
});

describe("dadosFiscaisSchema — documento (FEAUTH-09)", () => {
  it("aceita CPF válido mascarado (paridade com Digitos.Apenas)", () => {
    const result = dadosFiscaisSchema.safeParse({
      ...enderecoBase,
      tipoDocumento: "Cpf",
      documento: "111.444.777-35",
    });
    expect(result.success).toBe(true);
  });

  it("rejeita CPF com DV inválido no campo documento", () => {
    const result = dadosFiscaisSchema.safeParse({
      ...enderecoBase,
      tipoDocumento: "Cpf",
      documento: "111.444.777-36",
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues.some((i) => i.path.join(".") === "documento")).toBe(true);
    }
  });

  it("aceita CNPJ válido mascarado", () => {
    const result = dadosFiscaisSchema.safeParse({
      ...enderecoBase,
      tipoDocumento: "Cnpj",
      documento: "11.222.333/0001-81",
    });
    expect(result.success).toBe(true);
  });

  it("rejeita CNPJ com DV inválido no campo documento", () => {
    const result = dadosFiscaisSchema.safeParse({
      ...enderecoBase,
      tipoDocumento: "Cnpj",
      documento: "11.222.333/0001-80",
    });
    expect(result.success).toBe(false);
  });

  it("reavalia DV ao trocar tipoDocumento (EC-8)", () => {
    const comoCpf = dadosFiscaisSchema.safeParse({
      ...enderecoBase,
      tipoDocumento: "Cpf",
      documento: "11222333000181",
    });
    expect(comoCpf.success).toBe(false);

    const comoCnpj = dadosFiscaisSchema.safeParse({
      ...enderecoBase,
      tipoDocumento: "Cnpj",
      documento: "11222333000181",
    });
    expect(comoCnpj.success).toBe(true);
  });
});
