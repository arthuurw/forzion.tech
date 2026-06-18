import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";

const drain = vi.fn();
const hookState = {
  items: [] as unknown[],
  pendingCount: 0,
  draining: false,
  enqueue: vi.fn(),
  drain,
};

vi.mock("@/hooks/useExecucaoRetryQueue", () => ({
  useExecucaoRetryQueue: () => hookState,
}));

import ExecucaoPendenteBanner from "./ExecucaoPendenteBanner";

describe("ExecucaoPendenteBanner", () => {
  beforeEach(() => {
    hookState.pendingCount = 0;
    hookState.draining = false;
    drain.mockReset();
  });

  it("nao renderiza quando fila vazia (EXOFF-16)", () => {
    hookState.pendingCount = 0;
    const { container } = render(<ExecucaoPendenteBanner />);
    expect(container).toBeEmptyDOMElement();
  });

  it("exibe contagem singular quando 1 pendente", () => {
    hookState.pendingCount = 1;
    render(<ExecucaoPendenteBanner />);
    expect(screen.getByText("1 treino aguardando envio")).toBeInTheDocument();
  });

  it("exibe contagem plural quando >1 pendente (EXOFF-16)", () => {
    hookState.pendingCount = 3;
    render(<ExecucaoPendenteBanner />);
    expect(screen.getByText("3 treinos aguardando envio")).toBeInTheDocument();
  });

  it("botao dispara drain (EXOFF-17)", () => {
    hookState.pendingCount = 2;
    render(<ExecucaoPendenteBanner />);
    fireEvent.click(screen.getByRole("button", { name: /Tentar enviar agora/i }));
    expect(drain).toHaveBeenCalledTimes(1);
  });

  it("botao desabilitado e rotulado durante draining", () => {
    hookState.pendingCount = 1;
    hookState.draining = true;
    render(<ExecucaoPendenteBanner />);
    const btn = screen.getByRole("button", { name: /Enviando/i });
    expect(btn).toBeDisabled();
  });
});
