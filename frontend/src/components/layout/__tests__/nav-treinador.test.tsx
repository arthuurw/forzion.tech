import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { NAV_BY_TIPO } from "../NavConfig";

const push = vi.fn();
const useMediaQueryMock = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace: vi.fn(), back: vi.fn() }),
  usePathname: () => "/treinador/alunos",
}));

vi.mock("@/lib/auth/context", () => ({
  useAuth: () => ({ user: { tipoConta: "Treinador", nome: "Coach" }, isLoading: false, logout: vi.fn() }),
  homeRouteFor: () => "/treinador/alunos",
}));

vi.mock("../AppHeader", () => ({ default: () => <div /> }));
vi.mock("@/components/seguranca/StepUpProvider", () => ({ default: () => null }));

vi.mock("@mui/material", async () => {
  const actual = await vi.importActual<typeof import("@mui/material")>("@mui/material");
  return { ...actual, useMediaQuery: () => useMediaQueryMock() };
});

import AppLayout from "../AppLayout";

describe("NavConfig — treinador", () => {
  it("treinadorNav tem 8 itens com Recebimentos e Plano marcados drawerOnly", () => {
    const nav = NAV_BY_TIPO.Treinador;
    expect(nav).toHaveLength(8);
    const recebimentos = nav.find((i) => i.href === "/treinador/pagamentos");
    const plano = nav.find((i) => i.href === "/treinador/plano");
    expect(recebimentos?.drawerOnly).toBe(true);
    expect(plano?.drawerOnly).toBe(true);
    expect(nav.filter((i) => !i.drawerOnly)).toHaveLength(6);
  });
});

describe("AppLayout — navegação treinador", () => {
  it("drawer desktop mostra os 8 itens incluindo Recebimentos e Plano", () => {
    useMediaQueryMock.mockReturnValue(false);
    render(<AppLayout>conteudo</AppLayout>);
    expect(screen.getByText("Recebimentos")).toBeInTheDocument();
    expect(screen.getByText("Plano")).toBeInTheDocument();
    expect(screen.getByText("Notas fiscais")).toBeInTheDocument();
  });

  it("bottom-nav mobile omite Recebimentos e Plano (6 itens)", () => {
    useMediaQueryMock.mockReturnValue(true);
    render(<AppLayout>conteudo</AppLayout>);
    const actions = screen.getAllByRole("button");
    const labels = actions.map((a) => a.getAttribute("aria-label"));
    expect(labels).toContain("Alunos");
    expect(labels).toContain("Notas fiscais");
    expect(labels).not.toContain("Recebimentos");
    expect(labels).not.toContain("Plano");
  });
});
