import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type {
  AssinaturaTreinadorResponse,
  PlanoPlataformaResponse,
  TrocarPlanoTreinadorResponse,
} from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
}));

vi.mock("@/lib/auth/context", () => ({
  useAuth: () => ({ logout: vi.fn() }),
}));

const ASSINATURA_ATIVA: AssinaturaTreinadorResponse = {
  assinaturaId: "ass-1",
  status: "Ativa",
  valor: 50,
  planoPlataformaId: "plano-basic",
  dataProximaCobranca: new Date(Date.now() + 15 * 24 * 60 * 60 * 1000).toISOString(),
  planoPlataformaIdAgendado: null,
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

function setupHandlers(
  assinatura: AssinaturaTreinadorResponse = ASSINATURA_ATIVA,
  planos: PlanoPlataformaResponse[] = [PLANO_BASIC, PLANO_PRO, PLANO_FREE],
) {
  server.use(
    http.get("*/treinador/plano/assinatura", () => HttpResponse.json(assinatura)),
    http.get("*/auth/planos", () => HttpResponse.json(planos)),
  );
}

describe("PlanoTreinadorPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("exibe plano atual e outros planos disponíveis", async () => {
    setupHandlers();
    const { default: Page } = await import("../treinador/plano/page");
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByText("Basic")).toBeInTheDocument();
    });
    expect(screen.getByText("Pro")).toBeInTheDocument();
    expect(screen.queryByText("Elite")).not.toBeInTheDocument();
  });

  it("abre dialog de confirmação ao clicar Trocar", async () => {
    setupHandlers();
    const { default: Page } = await import("../treinador/plano/page");
    render(<Page />);

    await waitFor(() => screen.getAllByText("Trocar")[0]);
    fireEvent.click(screen.getAllByText("Trocar")[0]);

    await waitFor(() => {
      expect(screen.getByText("Confirmar troca de plano")).toBeInTheDocument();
    });
  });

  it("downgrade exibe sucesso e agendamento sem pagamento", async () => {
    setupHandlers();
    const downgradeResp: TrocarPlanoTreinadorResponse = {
      tipo: "Downgrade",
      pagamentoId: null,
      valorPagamento: null,
      metodoPagamento: null,
      pixQrCode: null,
      pixQrCodeUrl: null,
      pixExpiracao: null,
      clientSecret: null,
      dataEfetivacao: new Date(Date.now() + 20 * 24 * 60 * 60 * 1000).toISOString(),
    };
    server.use(
      http.post("*/treinador/plano/trocar", () => HttpResponse.json(downgradeResp)),
    );

    const { default: Page } = await import("../treinador/plano/page");
    render(<Page />);

    await waitFor(() => screen.getAllByText("Trocar")[0]);
    // Click Trocar on Free (downgrade)
    const botoesTocar = screen.getAllByText("Trocar");
    fireEvent.click(botoesTocar[botoesTocar.length - 1]);

    await screen.findByText("Confirmar troca de plano");
    fireEvent.click(screen.getByText("Confirmar"));

    await waitFor(() => {
      expect(screen.getByText("Downgrade agendado")).toBeInTheDocument();
    });
  });

  it("upgrade exibe QR code Pix quando tipo=Upgrade", async () => {
    setupHandlers();
    const upgradeResp: TrocarPlanoTreinadorResponse = {
      tipo: "Upgrade",
      pagamentoId: "pag-1",
      valorPagamento: 25,
      metodoPagamento: "Pix",
      pixQrCode: "00020126fake-pix-upgrade",
      pixQrCodeUrl: null,
      pixExpiracao: null,
      clientSecret: null,
      dataEfetivacao: null,
    };
    server.use(
      http.post("*/treinador/plano/trocar", () => HttpResponse.json(upgradeResp)),
    );

    const { default: Page } = await import("../treinador/plano/page");
    render(<Page />);

    await waitFor(() => screen.getAllByText("Trocar")[0]);
    fireEvent.click(screen.getAllByText("Trocar")[0]);

    await screen.findByText("Confirmar");
    fireEvent.click(screen.getByText("Confirmar"));

    await waitFor(() => {
      expect(screen.getByText("Pagamento")).toBeInTheDocument();
      expect(screen.getByText("00020126fake-pix-upgrade")).toBeInTheDocument();
    });
  });

  it("exibe alerta de inadimplência quando status=Inadimplente", async () => {
    const assinaturaInadimplente: AssinaturaTreinadorResponse = {
      ...ASSINATURA_ATIVA,
      status: "Inadimplente",
    };
    setupHandlers(assinaturaInadimplente);
    const { default: Page } = await import("../treinador/plano/page");
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByText(/Assinatura inadimplente/i)).toBeInTheDocument();
    });
  });

  it("exibe erro ao falhar carregamento", async () => {
    server.use(
      http.get("*/treinador/plano/assinatura", () => HttpResponse.error()),
      http.get("*/auth/planos", () => HttpResponse.json([])),
    );
    const { default: Page } = await import("../treinador/plano/page");
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByText(/Erro ao carregar/i)).toBeInTheDocument();
    });
  });
});
