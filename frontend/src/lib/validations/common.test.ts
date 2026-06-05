import { describe, it, expect } from "vitest";
import {
  emailSchema,
  passwordSchema,
  registerPasswordSchema,
  nomeSchema,
  telefoneSchema,
  loginSchema,
  cadastroTreinadorSchema,
  cadastroAlunoSchema,
} from "@/lib/validations/common";

describe("emailSchema", () => {
  it("aceita email válido", () => {
    expect(emailSchema.safeParse("user@test.com").success).toBe(true);
  });
  it("rejeita email inválido", () => {
    expect(emailSchema.safeParse("nao-e-email").success).toBe(false);
  });
  it("rejeita vazio", () => {
    expect(emailSchema.safeParse("").success).toBe(false);
  });
});

describe("passwordSchema", () => {
  it("aceita senha com 8+ caracteres", () => {
    expect(passwordSchema.safeParse("12345678").success).toBe(true);
  });
  it("rejeita senha com menos de 8 caracteres", () => {
    expect(passwordSchema.safeParse("1234567").success).toBe(false);
  });
});

describe("registerPasswordSchema", () => {
  it("aceita senha com maiúscula, minúscula e número", () => {
    expect(registerPasswordSchema.safeParse("Senha123").success).toBe(true);
  });
  it("rejeita senha sem maiúscula", () => {
    expect(registerPasswordSchema.safeParse("senha123").success).toBe(false);
  });
  it("rejeita senha sem minúscula", () => {
    expect(registerPasswordSchema.safeParse("SENHA123").success).toBe(false);
  });
  it("rejeita senha sem número", () => {
    expect(registerPasswordSchema.safeParse("SenhaABC").success).toBe(false);
  });
  it("rejeita senha com menos de 8 caracteres", () => {
    expect(registerPasswordSchema.safeParse("Se1").success).toBe(false);
  });
  it("rejeita senha com mais de 72 caracteres", () => {
    expect(registerPasswordSchema.safeParse("Aa1" + "x".repeat(70)).success).toBe(false);
  });
});

describe("nomeSchema", () => {
  it("aceita nome válido", () => {
    expect(nomeSchema.safeParse("João Silva").success).toBe(true);
  });
  it("rejeita nome com 1 caractere", () => {
    expect(nomeSchema.safeParse("J").success).toBe(false);
  });
  it("rejeita nome com mais de 100 caracteres", () => {
    expect(nomeSchema.safeParse("A".repeat(101)).success).toBe(false);
  });
});

describe("telefoneSchema", () => {
  it("aceita telefone com 10 dígitos", () => {
    expect(telefoneSchema.safeParse("1199998888").success).toBe(true);
  });
  it("aceita telefone com 11 dígitos", () => {
    expect(telefoneSchema.safeParse("11999998888").success).toBe(true);
  });
  it("rejeita telefone com letras", () => {
    expect(telefoneSchema.safeParse("1199998abc").success).toBe(false);
  });
  it("aceita string vazia (opcional)", () => {
    expect(telefoneSchema.safeParse("").success).toBe(true);
  });
  it("aceita undefined (opcional)", () => {
    expect(telefoneSchema.safeParse(undefined).success).toBe(true);
  });
});

describe("loginSchema", () => {
  it("aceita par válido", () => {
    const result = loginSchema.safeParse({ email: "a@b.com", password: "12345678" });
    expect(result.success).toBe(true);
  });
  it("rejeita email inválido", () => {
    expect(loginSchema.safeParse({ email: "invalido", password: "12345678" }).success).toBe(false);
  });
  it("rejeita senha curta", () => {
    expect(loginSchema.safeParse({ email: "a@b.com", password: "123" }).success).toBe(false);
  });
});

describe("cadastroTreinadorSchema", () => {
  const base = {
    nome: "Carlos", email: "c@t.com", password: "Senha123", confirmPassword: "Senha123",
    planoPlataformaId: "plano-1", modoPagamentoAluno: "Plataforma" as const,
  };

  it("aceita dados válidos", () => {
    expect(cadastroTreinadorSchema.safeParse(base).success).toBe(true);
  });
  it("rejeita quando senhas não coincidem", () => {
    expect(cadastroTreinadorSchema.safeParse({ ...base, confirmPassword: "outrasenha" }).success).toBe(false);
  });
  it("rejeita senha sem maiúscula", () => {
    expect(cadastroTreinadorSchema.safeParse({ ...base, password: "senha123", confirmPassword: "senha123" }).success).toBe(false);
  });
  it("rejeita sem plano selecionado", () => {
    expect(cadastroTreinadorSchema.safeParse({ ...base, planoPlataformaId: "" }).success).toBe(false);
  });
});

describe("cadastroAlunoSchema", () => {
  const base = {
    nome: "João",
    email: "j@a.com",
    telefone: "11999998888",
    password: "Senha123",
    confirmPassword: "Senha123",
    diasDisponiveis: "3",
    tempoDisponivelMinutos: "60",
    finalidade: "Hipertrofia",
    nivelCondicionamento: "Iniciante",
  };

  it("aceita dados válidos completos", () => {
    expect(cadastroAlunoSchema.safeParse(base).success).toBe(true);
  });
  it("aceita com campos opcionais vazios", () => {
    const data = { ...base, focoTreino: "", limitacoesFisicas: "", doencas: "", observacoesAdicionais: "" };
    expect(cadastroAlunoSchema.safeParse(data).success).toBe(true);
  });
  it("rejeita sem telefone", () => {
    expect(cadastroAlunoSchema.safeParse({ ...base, telefone: "" }).success).toBe(false);
  });
  it("rejeita sem diasDisponiveis", () => {
    expect(cadastroAlunoSchema.safeParse({ ...base, diasDisponiveis: "" }).success).toBe(false);
  });
  it("rejeita sem finalidade", () => {
    expect(cadastroAlunoSchema.safeParse({ ...base, finalidade: "" }).success).toBe(false);
  });
  it("rejeita quando senhas não coincidem", () => {
    expect(cadastroAlunoSchema.safeParse({ ...base, confirmPassword: "outra" }).success).toBe(false);
  });
  it("rejeita senha sem número", () => {
    expect(cadastroAlunoSchema.safeParse({ ...base, password: "SenhaABC", confirmPassword: "SenhaABC" }).success).toBe(false);
  });
});
