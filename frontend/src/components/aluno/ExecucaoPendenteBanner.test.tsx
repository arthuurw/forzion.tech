import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import type { RetryQueueFailure } from "@/hooks/useExecucaoRetryQueue";

const drain = vi.fn();
const hookState = {
  items: [] as unknown[],
  pendingCount: 0,
  draining: false,
  enqueue: vi.fn(),
  drain,
};
let capturedOnError: ((failure: RetryQueueFailure) => void) | undefined;

vi.mock("@/hooks/useExecucaoRetryQueue", () => ({
  useExecucaoRetryQueue: (opts?: { onError?: (failure: RetryQueueFailure) => void }) => {
    capturedOnError = opts?.onError;
    return hookState;
  },
}));

import ExecucaoPendenteBanner from "./ExecucaoPendenteBanner";

function failure(over: Partial<RetryQueueFailure>): RetryQueueFailure {
  return {
    item: { idempotencyKey: "k", payload: {} as never, alunoId: "a", treinoId: "t", enqueuedAt: 0 },
    status: 403,
    message: null,
    permanent: true,
    ...over,
  };
}

describe("ExecucaoPendenteBanner", () => {
  beforeEach(() => {
    hookState.pendingCount = 0;
    hookState.draining = false;
    drain.mockReset();
    capturedOnError = undefined;
  });

  it("nao renderiza quando fila vazia", () => {
    hookState.pendingCount = 0;
    const { container } = render(<ExecucaoPendenteBanner />);
    expect(container).toBeEmptyDOMElement();
  });

  it("exibe contagem singular quando 1 pendente", () => {
    hookState.pendingCount = 1;
    render(<ExecucaoPendenteBanner />);
    expect(screen.getByText("1 treino aguardando envio")).toBeInTheDocument();
  });

  it("exibe contagem plural quando >1 pendente", () => {
    hookState.pendingCount = 3;
    render(<ExecucaoPendenteBanner />);
    expect(screen.getByText("3 treinos aguardando envio")).toBeInTheDocument();
  });

  it("botao dispara drain", () => {
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

  it("falha permanente exibe aviso de descarte com a mensagem do servidor", () => {
    hookState.pendingCount = 0;
    render(<ExecucaoPendenteBanner />);
    act(() => capturedOnError?.(failure({ status: 403, message: "Vínculo inativo." })));
    expect(screen.getByRole("alert")).toHaveTextContent("Vínculo inativo.");
  });

  it("falha permanente sem mensagem usa texto padrão", () => {
    hookState.pendingCount = 0;
    render(<ExecucaoPendenteBanner />);
    act(() => capturedOnError?.(failure({ message: null })));
    expect(screen.getByRole("alert")).toHaveTextContent("recusado pelo servidor");
  });
});
