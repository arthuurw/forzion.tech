import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";

vi.mock("@/lib/api/aluno", () => ({
  alunoApi: { criarExecucao: vi.fn() },
}));

import { alunoApi, type CriarExecucaoData } from "@/lib/api/aluno";
import { useExecucaoRetryQueue, type RetryQueueItem } from "@/hooks/useExecucaoRetryQueue";

const criarExecucao = vi.mocked(alunoApi.criarExecucao);

function payload(treinoId: string): CriarExecucaoData {
  return { treinoId, dataExecucao: "2026-06-01", exercicios: [] };
}

function item(key: string, treinoId: string): Omit<RetryQueueItem, "enqueuedAt"> {
  return { idempotencyKey: key, payload: payload(treinoId), alunoId: "a1", treinoId };
}

function netError() {
  return Object.assign(new Error("Network Error"), {});
}

function httpError(status: number) {
  return { response: { status, data: { detail: `erro ${status}` } } };
}

describe("useExecucaoRetryQueue", () => {
  beforeEach(() => {
    localStorage.clear();
    criarExecucao.mockReset();
  });
  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it("enqueue adiciona item e incrementa pendingCount", () => {
    const { result } = renderHook(() => useExecucaoRetryQueue());
    act(() => result.current.enqueue(item("k1", "t1")));
    expect(result.current.pendingCount).toBe(1);
    expect(JSON.parse(localStorage.getItem("exec-queue")!)).toHaveLength(1);
  });

  it("drain sucesso remove item, chama onSuccess e reusa Idempotency-Key (EXOFF-11/12)", async () => {
    criarExecucao.mockResolvedValue({ data: {} } as never);
    const onSuccess = vi.fn();
    const { result } = renderHook(() => useExecucaoRetryQueue({ onSuccess }));
    act(() => result.current.enqueue(item("k1", "t1")));
    await act(async () => {
      await result.current.drain();
    });
    expect(criarExecucao).toHaveBeenCalledWith(payload("t1"), { idempotencyKey: "k1" });
    expect(onSuccess).toHaveBeenCalledWith("t1");
    expect(result.current.pendingCount).toBe(0);
    expect(localStorage.getItem("exec-queue")).toBeNull();
  });

  it("erro transitório (sem response) mantém item e para o drain (EXOFF-13/14)", async () => {
    criarExecucao.mockRejectedValueOnce(netError());
    const { result } = renderHook(() => useExecucaoRetryQueue());
    act(() => {
      result.current.enqueue(item("k1", "t1"));
      result.current.enqueue(item("k2", "t2"));
    });
    await act(async () => {
      await result.current.drain();
    });
    expect(criarExecucao).toHaveBeenCalledTimes(1);
    expect(result.current.pendingCount).toBe(2);
  });

  it("erro 5xx é transitório (mantém item)", async () => {
    criarExecucao.mockRejectedValueOnce(httpError(503));
    const { result } = renderHook(() => useExecucaoRetryQueue());
    act(() => result.current.enqueue(item("k1", "t1")));
    await act(async () => {
      await result.current.drain();
    });
    expect(result.current.pendingCount).toBe(1);
    expect(result.current.items[0].lastError).toBeUndefined();
  });

  it("erro permanente (403/422) mantém item com lastError e continua para o próximo (EXOFF-15)", async () => {
    criarExecucao
      .mockRejectedValueOnce(httpError(422))
      .mockResolvedValueOnce({ data: {} } as never);
    const onSuccess = vi.fn();
    const { result } = renderHook(() => useExecucaoRetryQueue({ onSuccess }));
    act(() => {
      result.current.enqueue(item("k1", "t1"));
      result.current.enqueue(item("k2", "t2"));
    });
    await act(async () => {
      await result.current.drain();
    });
    expect(criarExecucao).toHaveBeenCalledTimes(2);
    expect(result.current.pendingCount).toBe(1);
    expect(result.current.items[0].idempotencyKey).toBe("k1");
    expect(result.current.items[0].lastError).toBe("erro 422");
    expect(onSuccess).toHaveBeenCalledWith("t2");
  });

  it("processa múltiplos itens em ordem", async () => {
    criarExecucao.mockResolvedValue({ data: {} } as never);
    const { result } = renderHook(() => useExecucaoRetryQueue());
    act(() => {
      result.current.enqueue(item("k1", "t1"));
      result.current.enqueue(item("k2", "t2"));
    });
    await act(async () => {
      await result.current.drain();
    });
    expect(criarExecucao.mock.calls[0][1]).toEqual({ idempotencyKey: "k1" });
    expect(criarExecucao.mock.calls[1][1]).toEqual({ idempotencyKey: "k2" });
    expect(result.current.pendingCount).toBe(0);
  });

  it("drain no mount processa fila pré-existente", async () => {
    localStorage.setItem(
      "exec-queue",
      JSON.stringify([{ ...item("k1", "t1"), enqueuedAt: Date.now() }]),
    );
    criarExecucao.mockResolvedValue({ data: {} } as never);
    const { result } = renderHook(() => useExecucaoRetryQueue());
    await waitFor(() => expect(result.current.pendingCount).toBe(0));
    expect(criarExecucao).toHaveBeenCalledWith(payload("t1"), { idempotencyKey: "k1" });
  });

  it("fila com JSON corrompido → pendingCount 0 sem lançar", () => {
    localStorage.setItem("exec-queue", "{corrompido");
    const { result } = renderHook(() => useExecucaoRetryQueue());
    expect(result.current.pendingCount).toBe(0);
  });

  it("fila não-array no storage → tratada como vazia", () => {
    localStorage.setItem("exec-queue", JSON.stringify({ nao: "array" }));
    const { result } = renderHook(() => useExecucaoRetryQueue());
    expect(result.current.pendingCount).toBe(0);
  });

  it("localStorage indisponível (SSR) → enqueue não quebra", () => {
    vi.stubGlobal("localStorage", undefined);
    expect(() => {
      const { result } = renderHook(() => useExecucaoRetryQueue());
      act(() => result.current.enqueue(item("k1", "t1")));
    }).not.toThrow();
    vi.unstubAllGlobals();
  });

  it("erro permanente sem mensagem usa fallback 'Erro {status}'", async () => {
    criarExecucao.mockRejectedValueOnce({ response: { status: 422, data: {} } });
    const { result } = renderHook(() => useExecucaoRetryQueue());
    act(() => result.current.enqueue(item("k1", "t1")));
    await act(async () => {
      await result.current.drain();
    });
    expect(result.current.items[0].lastError).toBe("Erro 422");
  });

  it("evento window 'online' dispara drain", async () => {
    criarExecucao.mockResolvedValue({ data: {} } as never);
    const { result } = renderHook(() => useExecucaoRetryQueue());
    act(() => result.current.enqueue(item("k1", "t1")));
    await act(async () => {
      window.dispatchEvent(new Event("online"));
    });
    await waitFor(() => expect(result.current.pendingCount).toBe(0));
  });
});
