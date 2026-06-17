import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { PlanoPlataformaResponse, TreinadorResponse } from "@/types";

const PLANO_BASIC: PlanoPlataformaResponse = {
  planoId: "plano-basic", nome: "Basic", tier: "Basic", descricao: null, maxAlunos: 10, preco: 50, isAtivo: true,
};
const PLANO_FREE: PlanoPlataformaResponse = {
  planoId: "plano-free", nome: "Free", tier: "Free", descricao: null, maxAlunos: 5, preco: 0, isAtivo: true,
};

function planosHandler(planos: PlanoPlataformaResponse[] = [PLANO_BASIC, PLANO_FREE]) {
  server.use(http.get("*/auth/planos", () => HttpResponse.json(planos)));
}

function preencherDados() {
  fireEvent.change(screen.getByLabelText(/Nome completo/i), { target: { value: "Maria Treinadora" } });
  fireEvent.change(screen.getByLabelText(/E-mail/i), { target: { value: "maria@ex.com" } });
  fireEvent.change(screen.getByLabelText(/^Senha/i), { target: { value: "Senha123" } });
  fireEvent.change(screen.getByLabelText(/Confirmar senha/i), { target: { value: "Senha123" } });
}

async function importPage() {
  const { default: Page } = await import("../cadastro/treinador/page");
  return Page;
}

describe("CadastroTreinadorPage (wizard)", () => {
  beforeEach(() => vi.clearAllMocks());

  it("carrega e exibe planos e opções de modo", async () => {
    planosHandler();
    const Page = await importPage();
    render(<Page />);

    expect(await screen.findByText(/Basic — /)).toBeInTheDocument();
    expect(screen.getByText(/Free — Grátis/)).toBeInTheDocument();
    expect(screen.getByText(/Pela plataforma/)).toBeInTheDocument();
    expect(screen.getByText(/Por fora/)).toBeInTheDocument();
  });

  it("plano Free finaliza em análise sem etapa de pagamento", async () => {
    planosHandler();
    const treinador: TreinadorResponse = {
      treinadorId: "t-1", nome: "Maria", contaId: "c-1", status: "AguardandoAprovacao",
      planoPlataformaId: "plano-free", createdAt: new Date().toISOString(),
    };
    server.use(http.post("*/auth/register/treinador", () => HttpResponse.json(treinador, { status: 201 })));

    const Page = await importPage();
    render(<Page />);
    await screen.findByText(/Free — Grátis/);

    preencherDados();
    fireEvent.click(screen.getByRole("radio", { name: /Free/ }));
    fireEvent.click(screen.getByRole("button", { name: /Continuar/ }));

    expect(await screen.findByText(/Solicitação enviada/)).toBeInTheDocument();
    expect(screen.getByText(/em análise/i)).toBeInTheDocument();
  });

  it("plano pago avança para pagamento e exibe Pix sem polling", async () => {
    planosHandler();
    const treinador: TreinadorResponse = {
      treinadorId: "t-9", nome: "Maria", contaId: "c-1", status: "AguardandoPagamento",
      planoPlataformaId: "plano-basic", createdAt: new Date().toISOString(),
    };
    server.use(
      http.post("*/auth/register/treinador", () => HttpResponse.json(treinador, { status: 201 })),
      http.post("*/auth/treinador/:id/pagamento", () =>
        HttpResponse.json({
          pagamentoId: "pg-1", valor: 50, status: "Pendente", metodoPagamento: "Pix",
          stripePaymentIntentId: null, pixQrCode: "00020126fake-pix", pixQrCodeUrl: null,
          pixExpiracao: null, clientSecret: null, createdAt: new Date().toISOString(),
        }),
      ),
    );

    const Page = await importPage();
    render(<Page />);
    await screen.findByText(/Basic — /);

    preencherDados();
    fireEvent.click(screen.getByRole("radio", { name: /Basic/ }));
    fireEvent.click(screen.getByRole("button", { name: /Continuar/ }));

    expect(await screen.findByText(/Pagamento do plano/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /Pagar com Pix/ }));

    expect(await screen.findByText(/Pague via Pix/)).toBeInTheDocument();
    expect(screen.getByText(/00020126fake-pix/)).toBeInTheDocument();
  });

  it("exibe transparência CDC art. 31 antes do CTA de pagamento (R6)", async () => {
    planosHandler();
    const treinador: TreinadorResponse = {
      treinadorId: "t-9", nome: "Maria", contaId: "c-1", status: "AguardandoPagamento",
      planoPlataformaId: "plano-basic", createdAt: new Date().toISOString(),
    };
    server.use(http.post("*/auth/register/treinador", () => HttpResponse.json(treinador, { status: 201 })));

    const Page = await importPage();
    render(<Page />);
    await screen.findByText(/Basic — /);

    preencherDados();
    fireEvent.click(screen.getByRole("radio", { name: /Basic/ }));
    fireEvent.click(screen.getByRole("button", { name: /Continuar/ }));

    expect(await screen.findByText(/Pagamento do plano/)).toBeInTheDocument();
    expect(screen.getByText(/R\$\s?50,00/)).toBeInTheDocument();
    expect(screen.getByText(/mensal/i)).toBeInTheDocument();
    expect(screen.getByText(/próxima cobrança/i)).toBeInTheDocument();
    expect(screen.getByText(/7 dias/i)).toBeInTheDocument();
    expect(screen.getByText(/reembolso/i)).toBeInTheDocument();
  });

  it("falha ao carregar planos exibe erro", async () => {
    server.use(http.get("*/auth/planos", () => HttpResponse.error()));
    const Page = await importPage();
    render(<Page />);

    expect(await screen.findByText(/Não foi possível carregar os planos/)).toBeInTheDocument();
  });
});
