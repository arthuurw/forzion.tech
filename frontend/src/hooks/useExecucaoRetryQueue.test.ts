import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";

vi.mock("@/lib/api/aluno", () => ({
  alunoApi: { criarExecucao: vi.fn() },
}));

import { alunoApi, type CriarExecucaoData } from "@/lib/api/aluno";
import { useExecucaoRetryQueue, type RetryQueueItem } from "@/hooks/useExecucaoRetryQueue";
import { __resetRetryQueueStore } from "@/lib/execucao/retryQueueStore";

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
    __resetRetryQueueStore();
  });
  afterEach(() => {
    __resetRetryQueueStore();
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it("enqueue adiciona item e incrementa pendingCount", () => {
    const { result } = renderHook(() => useExecucaoRetryQueue());
    act(() => result.current.enqueue(item("k1", "t1")));
    expect(result.current.pendingCount).toBe(1);
    expect(JSON.parse(localStorage.getItem("exec-queue")!)).toHaveLength(1);
  });

  it("drain sucesso remove item, chama onSuccess e reusa Idempotency-Key", async () => {
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

  it("erro transitório (sem response) mantém item e para o drain", async () => {
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
    expect(result.current.items[0].attempts).toBe(1);
  });

  it("erro 5xx é transitório (mantém item e conta tentativa)", async () => {
    criarExecucao.mockRejectedValueOnce(httpError(503));
    const { result } = renderHook(() => useExecucaoRetryQueue());
    act(() => result.current.enqueue(item("k1", "t1")));
    await act(async () => {
      await result.current.drain();
    });
    expect(result.current.pendingCount).toBe(1);
    expect(result.current.items[0].attempts).toBe(1);
  });

  it("erro 429 é transitório (mantém item, não descarta)", async () => {
    criarExecucao.mockRejectedValueOnce(httpError(429));
    const { result } = renderHook(() => useExecucaoRetryQueue());
    act(() => result.current.enqueue(item("k1", "t1")));
    await act(async () => {
      await result.current.drain();
    });
    expect(result.current.pendingCount).toBe(1);
    expect(result.current.items[0].attempts).toBe(1);
  });

  it("erro permanente (4xx) descarta item, chama onError e continua para o próximo", async () => {
    criarExecucao
      .mockRejectedValueOnce(httpError(422))
      .mockResolvedValueOnce({ data: {} } as never);
    const onSuccess = vi.fn();
    const onError = vi.fn();
    const { result } = renderHook(() => useExecucaoRetryQueue({ onSuccess, onError }));
    act(() => {
      result.current.enqueue(item("k1", "t1"));
      result.current.enqueue(item("k2", "t2"));
    });
    await act(async () => {
      await result.current.drain();
    });
    expect(criarExecucao).toHaveBeenCalledTimes(2);
    expect(result.current.pendingCount).toBe(0);
    expect(onError).toHaveBeenCalledWith(
      expect.objectContaining({ permanent: true, status: 422, message: "erro 422" }),
    );
    expect(onSuccess).toHaveBeenCalledWith("t2");
  });

  it("erro permanente sem mensagem chama onError com message null", async () => {
    criarExecucao.mockRejectedValueOnce({ response: { status: 422, data: {} } });
    const onError = vi.fn();
    const { result } = renderHook(() => useExecucaoRetryQueue({ onError }));
    act(() => result.current.enqueue(item("k1", "t1")));
    await act(async () => {
      await result.current.drain();
    });
    expect(result.current.pendingCount).toBe(0);
    expect(onError).toHaveBeenCalledWith(
      expect.objectContaining({ permanent: true, status: 422, message: null }),
    );
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

  it("enqueue numa instância reflete em outra (store compartilhado)", () => {
    const { result: resultA } = renderHook(() => useExecucaoRetryQueue());
    const { result: resultB } = renderHook(() => useExecucaoRetryQueue());
    act(() => resultA.current.enqueue(item("k1", "t1")));
    expect(resultB.current.pendingCount).toBe(1);
  });

  it("instâncias concorrentes não duplicam POST do mesmo item", async () => {
    criarExecucao.mockImplementation(
      () => new Promise((resolve) => setTimeout(() => resolve({ data: {} } as never), 0)),
    );
    const { result: resultA } = renderHook(() => useExecucaoRetryQueue());
    const { result: resultB } = renderHook(() => useExecucaoRetryQueue());
    act(() => resultA.current.enqueue(item("k1", "t1")));
    await act(async () => {
      await Promise.all([resultA.current.drain(), resultB.current.drain()]);
    });
    expect(criarExecucao).toHaveBeenCalledTimes(1);
  });

  it("fila com JSON corrompido → pendingCount 0 sem lançar", () => {
    localStorage.setItem("exec-queue", "{corrompido");
    __resetRetryQueueStore();
    const { result } = renderHook(() => useExecucaoRetryQueue());
    expect(result.current.pendingCount).toBe(0);
  });

  it("fila não-array no storage → tratada como vazia", () => {
    localStorage.setItem("exec-queue", JSON.stringify({ nao: "array" }));
    __resetRetryQueueStore();
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

  it("enqueue arma retry automático (backoff dispara drain sem ação do usuário)", async () => {
    vi.useFakeTimers();
    criarExecucao.mockResolvedValue({ data: {} } as never);
    const { result } = renderHook(() => useExecucaoRetryQueue());
    act(() => result.current.enqueue(item("k1", "t1")));
    expect(criarExecucao).not.toHaveBeenCalled();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(5000);
    });
    expect(criarExecucao).toHaveBeenCalledWith(payload("t1"), { idempotencyKey: "k1" });
    expect(result.current.pendingCount).toBe(0);
    vi.useRealTimers();
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
