import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import AppHeader from "../AppHeader";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock("@/lib/auth/context", () => ({
  useAuth: () => ({ user: { nome: "Coach", tipoConta: "Treinador" }, logout: vi.fn() }),
  homeRouteFor: () => "/treinador/alunos",
}));

describe("AppHeader — a11y do menu (FPAD-05)", () => {
  it("trigger do menu tem nome acessível, aria-haspopup e aria-expanded refletindo o estado", () => {
    render(<AppHeader />);

    const trigger = screen.getByRole("button", { name: "Abrir menu do usuário" });
    expect(trigger).toHaveAttribute("aria-haspopup", "true");
    expect(trigger).toHaveAttribute("aria-expanded", "false");

    fireEvent.click(trigger);
    expect(trigger).toHaveAttribute("aria-expanded", "true");
  });
});
