import { describe, it, expect } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { ReactNode } from "react";
import type { TreinadorDashboardResponse } from "@/types";
import { useTreinadorDashboard } from "../useTreinadorDashboard";

const DASHBOARD: TreinadorDashboardResponse = {
  counts: { ativos: 1, aguardando: 0, inativos: 0 },
  mrr: 0,
  receitaPorPacote: [],
  totalFichas: 0,
  objetivos: [],
  pendentes: [],
  onboarding: { onboardingCompleto: true, contaConfigurada: true, modoPagamentoAluno: "Plataforma", modoPagamentoPodeAlterarEm: null },
  plano: {
    status: "Ativa",
    tierEfetivo: "Pro",
    planoContratadoId: null,
    alunosAtivos: 1,
    capEfetivo: 30,
    excedente: 0,
    gracaAte: null,
    temCortesia: false,
  },
  dadosFiscaisPendentes: false,
};

function wrapperWithClient(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe("useTreinadorDashboard", () => {
  it("retorna os dados do dashboard após resolver a query", async () => {
    server.use(http.get("*/treinador/dashboard", () => HttpResponse.json(DASHBOARD)));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    const { result } = renderHook(() => useTreinadorDashboard(), { wrapper: wrapperWithClient(client) });

    await waitFor(() => expect(result.current.isPending).toBe(false));
    expect(result.current.data).toEqual(DASHBOARD);
  });

  it("dois consumidores no mesmo QueryClient compartilham 1 única chamada de rede (G-FE-3)", async () => {
    let calls = 0;
    server.use(
      http.get("*/treinador/dashboard", () => {
        calls++;
        return HttpResponse.json(DASHBOARD);
      }),
    );
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const wrapper = wrapperWithClient(client);

    const { result: result1 } = renderHook(() => useTreinadorDashboard(), { wrapper });
    const { result: result2 } = renderHook(() => useTreinadorDashboard(), { wrapper });

    await waitFor(() => expect(result1.current.isPending).toBe(false));
    await waitFor(() => expect(result2.current.isPending).toBe(false));

    expect(calls).toBe(1);
    expect(result2.current.data).toEqual(DASHBOARD);
  });
});
