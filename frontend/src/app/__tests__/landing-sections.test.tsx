import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import { buildPlano } from "@/test/factories/plano";
import { renderLanding, setupLandingTest } from "@/test/helpers/landing";

setupLandingTest();

describe("LandingPage — hero R1/R2", () => {
  it("hero tem botão 'Criar conta grátis'", async () => {
    await renderLanding([]);
    expect(screen.getByRole("button", { name: /criar conta grátis/i })).toBeInTheDocument();
  }, 20000);

  it("hero tem link discreto para aluno", async () => {
    await renderLanding([]);
    expect(screen.getByText(/Acesse aqui como aluno/i)).toBeInTheDocument();
  }, 20000);

  it("subheadline variante A presente", async () => {
    await renderLanding([]);
    expect(screen.getByText(/Pare de perder tempo com planilha e WhatsApp/i)).toBeInTheDocument();
  }, 20000);
});

describe("LandingPage — seções montadas", () => {
  it("seção 'Como funciona' presente", async () => {
    await renderLanding([]);
    expect(screen.getByText("Uma estrutura pensada para o dia a dia")).toBeInTheDocument();
  }, 20000);

  it("seção Diferenciais presente", async () => {
    await renderLanding([]);
    expect(screen.getByText("O que nos diferencia")).toBeInTheDocument();
  }, 20000);

  it("FAQ presente", async () => {
    await renderLanding([]);
    expect(screen.getByText("Meu aluno precisa pagar para usar?")).toBeInTheDocument();
  }, 20000);
});

describe("LandingPage — descrição do plano (fonte: seed)", () => {
  it("renderiza a descrição completa vinda do plano", async () => {
    await renderLanding([
      buildPlano({ tier: "Pro", nome: "Pro", preco: 100, descricao: "Tudo do Basic + notificações por e-mail." }),
    ]);
    expect(screen.getByText("Tudo do Basic + notificações por e-mail.")).toBeInTheDocument();
  }, 20000);

  it("plano inativo exibe selo 'Em breve' e mantém a descrição", async () => {
    await renderLanding([
      buildPlano({ tier: "Elite", nome: "Elite", preco: 500, isAtivo: false, descricao: "O plano mais completo: tudo do Pro Plus somado a IA." }),
    ]);
    expect(screen.getByText("Em breve")).toBeInTheDocument();
    expect(screen.getByText("O plano mais completo: tudo do Pro Plus somado a IA.")).toBeInTheDocument();
  }, 20000);

  it("plano ativo não exibe 'Em breve'", async () => {
    await renderLanding([buildPlano({ tier: "Pro", nome: "Pro", preco: 100, isAtivo: true })]);
    expect(screen.queryByText("Em breve")).not.toBeInTheDocument();
  }, 20000);
});

describe("LandingPage — dedupe cancelamento R8", () => {
  it("aviso CDC aparece exatamente uma vez para múltiplos planos pagos", async () => {
    await renderLanding([
      buildPlano({ tier: "Basic", nome: "Basic", preco: 50 }),
      buildPlano({ tier: "Pro", nome: "Pro", preco: 100 }),
    ]);
    const notices = screen.getAllByText(/Cobrança mensal recorrente/);
    expect(notices).toHaveLength(1);
  }, 20000);

  it("aviso CDC não renderiza quando todos os planos são gratuitos", async () => {
    await renderLanding([buildPlano({ tier: "Free", nome: "Free", preco: 0 })]);
    expect(screen.queryByText(/Cobrança mensal recorrente/)).not.toBeInTheDocument();
  }, 20000);
});
