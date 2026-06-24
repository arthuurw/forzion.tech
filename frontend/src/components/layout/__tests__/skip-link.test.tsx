import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { renderWithProviders } from "@/test/render";

const push = vi.fn();
const replace = vi.fn();
const useMediaQueryMock = vi.fn();
const authState: { user: { tipoConta: string; nome: string } | null; isLoading: boolean; logout: ReturnType<typeof vi.fn> } = {
  user: { tipoConta: "Treinador", nome: "Coach" },
  isLoading: false,
  logout: vi.fn(),
};

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace, back: vi.fn() }),
  usePathname: () => "/treinador/alunos",
}));

vi.mock("@/lib/auth/context", () => ({
  useAuth: () => authState,
  homeRouteFor: () => "/treinador/alunos",
}));

vi.mock("@/hooks/useInactivity", () => ({
  useInactivity: () => {},
}));

vi.mock("../AppHeader", () => ({
  default: () => null,
}));
vi.mock("@/components/seguranca/StepUpProvider", () => ({ default: () => null }));

vi.mock("@mui/material", async () => {
  const actual = await vi.importActual<typeof import("@mui/material")>("@mui/material");
  return { ...actual, useMediaQuery: () => useMediaQueryMock() };
});

import AppLayout from "../AppLayout";
import PublicLayout from "../PublicLayout";

beforeEach(() => {
  authState.user = { tipoConta: "Treinador", nome: "Coach" };
  authState.isLoading = false;
  useMediaQueryMock.mockReturnValue(false);
});

describe("skip-to-content landmark target (WCAG 2.4.1)", () => {
  it("AppLayout expõe um <main id=main-content> programaticamente focável", () => {
    render(<AppLayout>conteudo</AppLayout>);
    const main = screen.getByRole("main");
    expect(main).toHaveAttribute("id", "main-content");
    expect(main).toHaveAttribute("tabindex", "-1");
  });

  it("PublicLayout desktop expõe um <main id=main-content> programaticamente focável", () => {
    useMediaQueryMock.mockReturnValue(false);
    renderWithProviders(<PublicLayout>conteudo</PublicLayout>, { skipAuth: true });
    const main = screen.getByRole("main");
    expect(main).toHaveAttribute("id", "main-content");
    expect(main).toHaveAttribute("tabindex", "-1");
  });

  it("PublicLayout mobile expõe um <main id=main-content> programaticamente focável", () => {
    useMediaQueryMock.mockReturnValue(true);
    renderWithProviders(<PublicLayout>conteudo</PublicLayout>, { skipAuth: true });
    const main = screen.getByRole("main");
    expect(main).toHaveAttribute("id", "main-content");
    expect(main).toHaveAttribute("tabindex", "-1");
  });
});
