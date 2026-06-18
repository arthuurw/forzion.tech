import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, act, waitFor } from "@testing-library/react";
import { NAV_BY_TIPO } from "../NavConfig";
import { ASSINATURA_INADIMPLENTE_EVENT } from "@/lib/api/client";

const push = vi.fn();
const replace = vi.fn();
const useMediaQueryMock = vi.fn();
const authState: { user: { tipoConta: string; nome: string } | null; isLoading: boolean; logout: ReturnType<typeof vi.fn> } = {
  user: { tipoConta: "Treinador", nome: "Coach" },
  isLoading: false,
  logout: vi.fn(),
};
let inactivity: { onWarn: (m: number) => void; onTimeout: () => void } = { onWarn: () => {}, onTimeout: () => {} };

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace, back: vi.fn() }),
  usePathname: () => "/treinador/alunos",
}));

vi.mock("@/lib/auth/context", () => ({
  useAuth: () => authState,
  homeRouteFor: () => "/treinador/alunos",
}));

vi.mock("@/hooks/useInactivity", () => ({
  useInactivity: (opts: typeof inactivity) => { inactivity = opts; },
}));

vi.mock("../AppHeader", () => ({
  default: ({ onMenuToggle }: { onMenuToggle: () => void }) => (
    <button aria-label="menu-toggle" onClick={onMenuToggle} />
  ),
}));
vi.mock("@/components/seguranca/StepUpProvider", () => ({ default: () => null }));

vi.mock("@mui/material", async () => {
  const actual = await vi.importActual<typeof import("@mui/material")>("@mui/material");
  return { ...actual, useMediaQuery: () => useMediaQueryMock() };
});

import AppLayout from "../AppLayout";

beforeEach(() => {
  push.mockClear();
  replace.mockClear();
  authState.user = { tipoConta: "Treinador", nome: "Coach" };
  authState.isLoading = false;
  authState.logout.mockClear();
  useMediaQueryMock.mockReturnValue(false);
});

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
    render(<AppLayout>conteudo</AppLayout>);
    expect(screen.getByText("Recebimentos")).toBeInTheDocument();
    expect(screen.getByText("Plano")).toBeInTheDocument();
    expect(screen.getByText("Notas fiscais")).toBeInTheDocument();
  });

  it("drawer desktop navega ao clicar num item exclusivo do drawer", () => {
    render(<AppLayout>conteudo</AppLayout>);
    fireEvent.click(screen.getByText("Recebimentos"));
    expect(push).toHaveBeenCalledWith("/treinador/pagamentos");
  });

  it("bottom-nav mobile omite Recebimentos e Plano (6 itens)", () => {
    useMediaQueryMock.mockReturnValue(true);
    render(<AppLayout>conteudo</AppLayout>);
    const labels = screen.getAllByRole("button").map((a) => a.getAttribute("aria-label"));
    expect(labels).toContain("Alunos");
    expect(labels).toContain("Notas fiscais");
    expect(labels).not.toContain("Recebimentos");
    expect(labels).not.toContain("Plano");
  });

  it("bottom-nav clica num item e navega pelo href do item filtrado", () => {
    useMediaQueryMock.mockReturnValue(true);
    render(<AppLayout>conteudo</AppLayout>);
    fireEvent.click(screen.getByLabelText("Fichas"));
    expect(push).toHaveBeenCalledWith("/treinador/treinos");
  });

  it("menu-toggle no desktop colapsa o drawer (oculta rótulos)", () => {
    render(<AppLayout>conteudo</AppLayout>);
    expect(screen.getByText("Recebimentos")).toBeInTheDocument();
    fireEvent.click(screen.getByLabelText("menu-toggle"));
    expect(screen.queryByText("Recebimentos")).not.toBeInTheDocument();
  });

  it("menu-toggle no mobile abre o drawer temporário e fecha no Escape", () => {
    useMediaQueryMock.mockReturnValue(true);
    render(<AppLayout>conteudo</AppLayout>);
    fireEvent.click(screen.getByLabelText("menu-toggle"));
    const drawerItem = screen.getByText("Recebimentos");
    expect(drawerItem).toBeInTheDocument();
    fireEvent.keyDown(drawerItem, { key: "Escape" });
    expect(push).not.toHaveBeenCalled();
  });
});

describe("AppLayout — toasts e sessão", () => {
  it("evento de inadimplência exibe toast e fecha ao clicar", async () => {
    render(<AppLayout>conteudo</AppLayout>);
    act(() => {
      window.dispatchEvent(
        new CustomEvent(ASSINATURA_INADIMPLENTE_EVENT, { detail: { message: "Pagamento em atraso." } }),
      );
    });
    expect(await screen.findByText("Pagamento em atraso.")).toBeInTheDocument();
    fireEvent.click(screen.getByLabelText("Close"));
    await waitFor(() => expect(screen.queryByText("Pagamento em atraso.")).not.toBeInTheDocument());
  });

  it("aviso de inatividade aparece e timeout faz logout", async () => {
    render(<AppLayout>conteudo</AppLayout>);
    act(() => inactivity.onWarn(2));
    expect(await screen.findByText(/sem atividade há 2 minutos/)).toBeInTheDocument();
    fireEvent.click(screen.getByLabelText("Close"));
    await waitFor(() => expect(screen.queryByText(/sem atividade há 2 minutos/)).not.toBeInTheDocument());
    act(() => inactivity.onTimeout());
    expect(authState.logout).toHaveBeenCalledOnce();
  });

  it("sem usuário autenticado limpa sessão server-side e redireciona para /login", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null));
    vi.stubGlobal("fetch", fetchMock);
    authState.user = null;
    render(<AppLayout>conteudo</AppLayout>);
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith("/api/auth/logout", { method: "POST" }));
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/login"));
    vi.unstubAllGlobals();
  });
});
