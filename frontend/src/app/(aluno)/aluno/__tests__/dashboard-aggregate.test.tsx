import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";
import type { AlunoDashboardResponse } from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

vi.mock("recharts", () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  BarChart: ({ children }: { children: React.ReactNode }) => <svg role="img">{children}</svg>,
  Bar: () => null,
  XAxis: () => null,
  YAxis: () => null,
  Tooltip: () => null,
  Legend: () => null,
}));

const semana = (iso: string, total: number) => ({
  semanaInicio: iso,
  semanaFim: iso,
  total,
});

const dashboard: AlunoDashboardResponse = {
  totalFichas: 3,
  fichasAtivas: [
    { treinoAlunoId: "ta-1", treinoId: "t-1", nomeTreino: "Treino Força", objetivo: "Forca", criadoEm: "2026-01-10T00:00:00Z" },
    { treinoAlunoId: "ta-2", treinoId: "t-2", nomeTreino: "Treino Hiper", objetivo: "Hipertrofia", criadoEm: "2026-02-01T00:00:00Z" },
  ],
  totalExecucoes: 42,
  sessoesPorSemana: [
    semana("2026-04-28T00:00:00Z", 0),
    semana("2026-05-05T00:00:00Z", 1),
    semana("2026-05-12T00:00:00Z", 0),
    semana("2026-05-19T00:00:00Z", 2),
    semana("2026-05-26T00:00:00Z", 0),
    semana("2026-06-02T00:00:00Z", 3),
    semana("2026-06-09T00:00:00Z", 0),
    semana("2026-06-16T00:00:00Z", 5),
  ],
  vinculo: { ativo: true, pendente: false },
};

describe("DashboardAlunoPage — agregado /aluno/dashboard (T6)", () => {
  beforeEach(() => {
    server.use(
      http.get("*/aluno/dashboard", () => HttpResponse.json(dashboard)),
    );
  });

  it("renderiza totalFichas e totalExecucoes do agregado", async () => {
    const { default: Page } = await import("@/app/(aluno)/aluno/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(screen.getByText("3")).toBeInTheDocument();
    });
    expect(screen.getByText("42")).toBeInTheDocument();
  });

  it("renderiza fichas ativas vindas do agregado", async () => {
    const { default: Page } = await import("@/app/(aluno)/aluno/page");
    renderWithProviders(<Page />, { skipAuth: true });

    expect(await screen.findByText("Treino Força")).toBeInTheDocument();
    expect(screen.getByText("Treino Hiper")).toBeInTheDocument();
  });

  it("histograma 8 semanas renderizado a partir de sessoesPorSemana", async () => {
    const { default: Page } = await import("@/app/(aluno)/aluno/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());
    expect(screen.getByText(/SESSÕES POR SEMANA/i)).toBeInTheDocument();
  });

  it("pie Fichas por status não existe no DOM (R3b — dropar)", async () => {
    const { default: Page } = await import("@/app/(aluno)/aluno/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());
    expect(screen.queryByText(/Fichas por status/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Inativas/i)).not.toBeInTheDocument();
  });

  it("banner SemVinculoAtivo não aparece quando vinculo.ativo=true", async () => {
    const { default: Page } = await import("@/app/(aluno)/aluno/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());
    expect(screen.queryByText(/não tem um vínculo ativo/)).not.toBeInTheDocument();
    expect(screen.queryByText(/aguardando aprovação/)).not.toBeInTheDocument();
  });

  it("banner SemVinculoAtivo exibe sem-vínculo quando vinculo={ativo:false,pendente:false}", async () => {
    server.use(
      http.get("*/aluno/dashboard", () =>
        HttpResponse.json({ ...dashboard, vinculo: { ativo: false, pendente: false } }),
      ),
    );

    const { default: Page } = await import("@/app/(aluno)/aluno/page");
    renderWithProviders(<Page />, { skipAuth: true });

    expect(await screen.findByText(/não tem um vínculo ativo/)).toBeInTheDocument();
  });

  it("banner SemVinculoAtivo exibe pendente quando vinculo={ativo:false,pendente:true}", async () => {
    server.use(
      http.get("*/aluno/dashboard", () =>
        HttpResponse.json({ ...dashboard, vinculo: { ativo: false, pendente: true } }),
      ),
    );

    const { default: Page } = await import("@/app/(aluno)/aluno/page");
    renderWithProviders(<Page />, { skipAuth: true });

    expect(await screen.findByText(/aguardando aprovação do treinador/)).toBeInTheDocument();
  });

  it("faz apenas 1 GET /aluno/dashboard — os 3 endpoints antigos não são chamados", async () => {
    let dashboardCalls = 0;
    let oldEndpointCalls = 0;

    server.use(
      http.get("*/aluno/dashboard", () => {
        dashboardCalls++;
        return HttpResponse.json(dashboard);
      }),
      http.get("*/aluno/fichas", () => {
        oldEndpointCalls++;
        return HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 1 });
      }),
      http.get("*/aluno/execucoes", () => {
        oldEndpointCalls++;
        return HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 1 });
      }),
      http.get("*/aluno/vinculo", () => {
        oldEndpointCalls++;
        return HttpResponse.json({ vinculoAtivo: null, vinculoPendente: null });
      }),
    );

    const { default: Page } = await import("@/app/(aluno)/aluno/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => expect(dashboardCalls).toBe(1));
    expect(oldEndpointCalls).toBe(0);
  });
});
