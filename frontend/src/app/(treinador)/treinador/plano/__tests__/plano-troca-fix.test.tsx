import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type {
  AssinaturaTreinadorResponse,
  PlanoPlataformaResponse,
} from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
}));

vi.mock("@/lib/auth/context", () => ({
  useAuth: () => ({ logout: vi.fn() }),
}));

const PLANO_ELITE: PlanoPlataformaResponse = {
  planoId: "plano-elite",
  nome: "Elite Max",
  tier: "Elite",
  descricao: null,
  maxAlunos: 999,
  preco: 300,
  isAtivo: true,
};

const PLANO_BASIC: PlanoPlataformaResponse = {
  planoId: "plano-basic",
  nome: "Basic",
  tier: "Basic",
  descricao: null,
  maxAlunos: 10,
  preco: 50,
  isAtivo: true,
};

const PLANO_PRO: PlanoPlataformaResponse = {
  planoId: "plano-pro",
  nome: "Pro",
  tier: "Pro",
  descricao: null,
  maxAlunos: 30,
  preco: 100,
  isAtivo: true,
};

const PLANO_FREE: PlanoPlataformaResponse = {
  planoId: "plano-free",
  nome: "Free",
  tier: "Free",
  descricao: null,
  maxAlunos: 5,
  preco: 0,
  isAtivo: true,
};

const ASSINATURA_BASE: AssinaturaTreinadorResponse = {
  assinaturaId: "ass-1",
  status: "Ativa",
  valor: 50,
  planoPlataformaId: "plano-basic",
  dataProximaCobranca: new Date(Date.now() + 15 * 24 * 60 * 60 * 1000).toISOString(),
  planoPlataformaIdAgendado: null,
};

function setupHandlers(
  assinatura: AssinaturaTreinadorResponse | null,
  planos: PlanoPlataformaResponse[] = [PLANO_BASIC, PLANO_PRO, PLANO_FREE],
) {
  server.use(
    http.get("*/treinador/plano/assinatura", () => HttpResponse.json(assinatura)),
    http.get("*/auth/planos", () => HttpResponse.json(planos)),
  );
}

async function renderPage() {
  const { default: Page } = await import("../page");
  render(<Page />);
}

describe("PlanoTreinadorPage — troca robusta (FPAD-01/02/03)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("FPAD-01.1: resolve nome do plano atual mesmo em tier Elite (lista crua)", async () => {
    setupHandlers(
      { ...ASSINATURA_BASE, planoPlataformaId: "plano-elite", valor: 300 },
      [PLANO_ELITE, PLANO_BASIC, PLANO_PRO],
    );
    await renderPage();

    expect(await screen.findByText("Elite Max")).toBeInTheDocument();
    expect(screen.queryByText("Plano atual")).not.toBeInTheDocument();
  });

  it("FPAD-02.2: classifica upgrade por assinatura.valor, não por planoAtual.preco", async () => {
    setupHandlers(
      { ...ASSINATURA_BASE, planoPlataformaId: "plano-elite", valor: 50 },
      [PLANO_ELITE, PLANO_BASIC, PLANO_PRO],
    );
    await renderPage();

    await waitFor(() => screen.getAllByRole("button", { name: "Trocar" }));
    const trocar = screen.getAllByRole("button", { name: "Trocar" });
    fireEvent.click(trocar[trocar.length - 1]);

    expect(await screen.findByText(/Upgrade para Pro/)).toBeInTheDocument();
  });

  it("FPAD-02.3: classifica downgrade quando novo preço < assinatura.valor", async () => {
    setupHandlers(
      { ...ASSINATURA_BASE, planoPlataformaId: "plano-basic", valor: 200 },
      [PLANO_BASIC, PLANO_PRO],
    );
    await renderPage();

    await waitFor(() => screen.getByRole("button", { name: "Trocar" }));
    fireEvent.click(screen.getByRole("button", { name: "Trocar" }));

    expect(await screen.findByText(/Downgrade para Pro/)).toBeInTheDocument();
  });

  it("FPAD-02.4: copy neutra quando não há base de preço (sem assinatura — contratar)", async () => {
    setupHandlers(null, [PLANO_BASIC, PLANO_PRO]);
    await renderPage();

    await waitFor(() => screen.getAllByRole("button", { name: "Contratar" }));
    const contratar = screen.getAllByRole("button", { name: "Contratar" });
    fireEvent.click(contratar[contratar.length - 1]);

    expect(await screen.findByText("Confirmar contratação")).toBeInTheDocument();
    expect(screen.getByText(/Contratar o plano Pro/)).toBeInTheDocument();
    expect(screen.queryByText(/Upgrade para Pro/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Downgrade para Pro/)).not.toBeInTheDocument();
  });

  it("FPAD-03.5: inadimplente inclui o plano corrente como opção selecionável", async () => {
    setupHandlers(
      { ...ASSINATURA_BASE, planoPlataformaId: "plano-basic", status: "Inadimplente" },
      [PLANO_BASIC, PLANO_PRO, PLANO_FREE],
    );
    await renderPage();

    await waitFor(() => screen.getAllByRole("button", { name: "Trocar" }));
    expect(screen.getAllByRole("button", { name: "Trocar" })).toHaveLength(3);
  });

  it("FPAD-01.6: caso normal (Ativa, não-Elite) exclui o plano corrente das opções", async () => {
    setupHandlers(
      { ...ASSINATURA_BASE, planoPlataformaId: "plano-basic", valor: 50 },
      [PLANO_BASIC, PLANO_PRO, PLANO_FREE],
    );
    await renderPage();

    await waitFor(() => screen.getAllByRole("button", { name: "Trocar" }));
    expect(screen.getAllByRole("button", { name: "Trocar" })).toHaveLength(2);

    fireEvent.click(screen.getAllByRole("button", { name: "Trocar" })[0]);
    expect(await screen.findByText(/Upgrade para Pro/)).toBeInTheDocument();
  });
});
