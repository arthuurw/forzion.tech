import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { buildPlano } from "@/test/factories/plano";

const originalFetch = global.fetch;

async function renderLanding(planos: ReturnType<typeof buildPlano>[]) {
  global.fetch = vi.fn().mockResolvedValue({
    ok: true,
    json: async () => planos,
  });
  const { default: LandingPage } = await import("@/app/page");
  const jsx = await LandingPage();
  render(jsx);
}

beforeEach(() => {
  vi.resetModules();
});

afterEach(() => {
  global.fetch = originalFetch;
});

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
    expect(screen.getByText("COMO FUNCIONA")).toBeInTheDocument();
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

describe("LandingPage — tier copy R7", () => {
  it("Free: copy correta", async () => {
    await renderLanding([buildPlano({ tier: "Free", nome: "Free", preco: 0 })]);
    expect(screen.getByText("Ideal para começar e testar sem compromisso")).toBeInTheDocument();
  }, 20000);

  it("Basic: copy correta", async () => {
    await renderLanding([buildPlano({ tier: "Basic", nome: "Basic", preco: 50 })]);
    expect(screen.getByText("R$2 por aluno/mês na lotação")).toBeInTheDocument();
  }, 20000);

  it("Pro: copy correta", async () => {
    await renderLanding([buildPlano({ tier: "Pro", nome: "Pro", preco: 100 })]);
    expect(screen.getByText("Notificações por e-mail mantêm seus alunos engajados entre sessões")).toBeInTheDocument();
  }, 20000);

  it("ProPlus: copy correta", async () => {
    await renderLanding([buildPlano({ tier: "ProPlus", nome: "Pro Plus", preco: 200 })]);
    expect(screen.getByText("WhatsApp integrado — seus alunos recebem tudo onde já estão")).toBeInTheDocument();
  }, 20000);

  it("Elite: sem copy de valor", async () => {
    await renderLanding([buildPlano({ tier: "Elite", nome: "Elite", preco: 500 })]);
    expect(screen.queryByText("Ideal para começar e testar sem compromisso")).not.toBeInTheDocument();
    expect(screen.queryByText("R$2 por aluno/mês na lotação")).not.toBeInTheDocument();
    expect(screen.queryByText(/WhatsApp integrado/)).not.toBeInTheDocument();
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
