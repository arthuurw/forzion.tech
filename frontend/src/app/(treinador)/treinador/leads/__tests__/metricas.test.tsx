import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { ListarLeadsResponse, ObterMetricasLeadsResponse } from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

function listResponse(): ListarLeadsResponse {
  return { items: [], total: 0, pagina: 1, tamanhoPagina: 20 };
}

function metricasResponse(overrides: Partial<ObterMetricasLeadsResponse> = {}): ObterMetricasLeadsResponse {
  return {
    total: 4,
    porOrigemAgente: 3,
    porOrigemManual: 1,
    convertidos: 1,
    taxaConversao: 0.25,
    porMotivoDescarte: {},
    ...overrides,
  };
}

async function renderPage() {
  const { default: Page } = await import("../page");
  render(<Page />);
}

describe("LeadsTreinadorPage — métricas", () => {
  beforeEach(() => {
    server.use(http.get("*/treinador/leads", () => HttpResponse.json(listResponse())));
  });

  it("carrega e exibe as métricas do período padrão", async () => {
    server.use(http.get("*/treinador/leads/metricas", () => HttpResponse.json(metricasResponse())));
    await renderPage();

    expect(await screen.findByText("4")).toBeInTheDocument();
    expect(screen.getByText("25.0%")).toBeInTheDocument();
  });

  it("trocar o período recalcula as métricas com um novo intervalo", async () => {
    let chamadas: { inicio: string | null; fim: string | null }[] = [];
    server.use(
      http.get("*/treinador/leads/metricas", ({ request }) => {
        const url = new URL(request.url);
        chamadas.push({ inicio: url.searchParams.get("inicio"), fim: url.searchParams.get("fim") });
        return HttpResponse.json(metricasResponse());
      }),
    );
    await renderPage();
    await screen.findByText("4");

    const chamadasAntes = chamadas.length;
    screen.getByRole("button", { name: "7 dias" }).click();

    await waitFor(() => expect(chamadas.length).toBeGreaterThan(chamadasAntes));
  });

  it("período sem leads exibe métricas zeradas sem quebrar a página", async () => {
    server.use(http.get("*/treinador/leads/metricas", () => HttpResponse.json(metricasResponse({
      total: 0, porOrigemAgente: 0, porOrigemManual: 0, convertidos: 0, taxaConversao: 0,
    }))));
    await renderPage();

    expect(await screen.findByText("0.0%")).toBeInTheDocument();
  });

  it("falha ao carregar métricas exibe alerta sem quebrar a lista", async () => {
    server.use(http.get("*/treinador/leads/metricas", () => HttpResponse.json({ title: "Erro" }, { status: 500 })));
    await renderPage();

    expect(await screen.findByText(/não foi possível carregar as métricas/i)).toBeInTheDocument();
    expect(await screen.findByText(/nenhum lead ainda/i)).toBeInTheDocument();
  });
});
