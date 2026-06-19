import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useExecucaoDraft, type DraftState } from "@/hooks/useExecucaoDraft";
import type { TreinoExercicioResponse } from "@/types";

const ALUNO = "a1";
const TREINO = "t1";

function ex(id: string): TreinoExercicioResponse {
  return {
    treinoExercicioId: id,
    exercicioId: `e-${id}`,
    nomeExercicio: `Ex ${id}`,
    ordem: 1,
    series: [
      {
        serieConfigId: `s-${id}`,
        quantidade: 2,
        repeticoesMin: 10,
        repeticoesMax: 12,
        descricao: null,
        carga: 20,
        descanso: 60,
        ordem: 1,
      },
    ],
  };
}

function draftState(): DraftState {
  return {
    execData: { te1: [{ reps: "10", carga: "20" }], te2: [{ reps: "8", carga: "30" }] },
    obsData: { te1: "ok" },
    observacao: "geral",
    currentIndex: 1,
  };
}

describe("useExecucaoDraft", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.runOnlyPendingTimers();
    vi.useRealTimers();
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it("autosave debounced grava draft após 500ms (EXOFF-01)", () => {
    const { result } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    act(() => result.current.save(draftState()));
    expect(localStorage.getItem(`exec-draft:${ALUNO}:${TREINO}`)).toBeNull();
    act(() => vi.advanceTimersByTime(500));
    const raw = localStorage.getItem(`exec-draft:${ALUNO}:${TREINO}`);
    expect(raw).not.toBeNull();
    const payload = JSON.parse(raw!);
    expect(payload.v).toBe(1);
    expect(payload.treinoExercicioIds).toEqual(["te1", "te2"]);
    expect(payload.observacao).toBe("geral");
    expect(result.current.hasDraft).toBe(true);
  });

  it("debounce coalesce: salva só o último estado", () => {
    const { result } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    act(() => result.current.save({ ...draftState(), observacao: "primeiro" }));
    act(() => vi.advanceTimersByTime(200));
    act(() => result.current.save({ ...draftState(), observacao: "segundo" }));
    act(() => vi.advanceTimersByTime(500));
    const payload = JSON.parse(localStorage.getItem(`exec-draft:${ALUNO}:${TREINO}`)!);
    expect(payload.observacao).toBe("segundo");
  });

  it("restore recupera o estado após reload (EXOFF-02)", () => {
    const { result: r1, unmount } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    act(() => r1.current.save(draftState()));
    act(() => vi.advanceTimersByTime(500));
    unmount();

    const { result: r2 } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    expect(r2.current.hasDraft).toBe(true);
    const restored = r2.current.restore();
    expect(restored).not.toBeNull();
    expect(restored!.observacao).toBe("geral");
    expect(restored!.currentIndex).toBe(1);
    expect(restored!.execData.te1[0].reps).toBe("10");
  });

  it("idempotencyKey é estável entre reloads e regenera no discard", () => {
    const { result: r1, unmount } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    const key1 = r1.current.idempotencyKey;
    act(() => r1.current.save(draftState()));
    act(() => vi.advanceTimersByTime(500));
    unmount();

    const { result: r2 } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    expect(r2.current.idempotencyKey).toBe(key1);
    act(() => r2.current.discard());
    expect(r2.current.idempotencyKey).not.toBe(key1);
  });

  it("reconcile dropa órfão e inicializa exercício novo (EXOFF-03)", () => {
    const { result } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    act(() => result.current.save(draftState()));
    act(() => vi.advanceTimersByTime(500));

    const r = result.current.reconcile([ex("te1"), ex("te3")]);
    expect(r).not.toBeNull();
    expect(Object.keys(r!.execData)).toEqual(["te1", "te3"]);
    expect(r!.execData.te1[0].reps).toBe("10");
    expect(r!.execData.te3).toHaveLength(2);
    expect(r!.reconciled).toBe(true);
  });

  it("reconcile sem divergência → reconciled false", () => {
    const { result } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    act(() => result.current.save({ ...draftState(), execData: { te1: [{ reps: "10", carga: "20" }] } }));
    act(() => vi.advanceTimersByTime(500));
    const r = result.current.reconcile([ex("te1")]);
    expect(r!.reconciled).toBe(false);
  });

  it("TTL: draft com mais de 48h é descartado e não restaura (EXOFF-04)", () => {
    const stale = {
      v: 1,
      idempotencyKey: "k",
      treinoExercicioIds: ["te1"],
      execData: { te1: [{ reps: "10", carga: "20" }] },
      obsData: {},
      observacao: "",
      currentIndex: 0,
      updatedAt: Date.now() - 49 * 60 * 60 * 1000,
    };
    localStorage.setItem(`exec-draft:${ALUNO}:${TREINO}`, JSON.stringify(stale));
    const { result } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    expect(result.current.hasDraft).toBe(false);
    expect(result.current.restore()).toBeNull();
    expect(localStorage.getItem(`exec-draft:${ALUNO}:${TREINO}`)).toBeNull();
  });

  it("discard limpa o draft (EXOFF-05)", () => {
    const { result } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    act(() => result.current.save(draftState()));
    act(() => vi.advanceTimersByTime(500));
    act(() => result.current.discard());
    expect(result.current.hasDraft).toBe(false);
    expect(result.current.restore()).toBeNull();
  });

  it("localStorage indisponível (setItem throw) → save no-op sem quebrar (EXOFF-06)", () => {
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("QuotaExceeded");
    });
    const { result } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    expect(() => {
      act(() => result.current.save(draftState()));
      act(() => vi.advanceTimersByTime(500));
    }).not.toThrow();
  });

  it("JSON corrompido → restore null sem lançar", () => {
    localStorage.setItem(`exec-draft:${ALUNO}:${TREINO}`, "{nao-json");
    const { result } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    expect(result.current.restore()).toBeNull();
    expect(result.current.reconcile([ex("te1")])).toBeNull();
  });

  it("crypto.randomUUID indisponível → idempotencyKey via fallback RFC4122", () => {
    vi.spyOn(crypto, "randomUUID").mockImplementation(() => {
      throw new Error("insecure context");
    });
    const { result } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    expect(result.current.idempotencyKey).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
    );
  });

  it("localStorage.getItem throw → hook inicia sem draft e não quebra", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("acesso negado");
    });
    const { result } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    expect(result.current.hasDraft).toBe(false);
    expect(result.current.idempotencyKey).toBeTruthy();
  });

  it("localStorage indisponível (SSR) → save/discard não quebram", () => {
    vi.stubGlobal("localStorage", undefined);
    expect(() => {
      const { result } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
      act(() => result.current.save(draftState()));
      act(() => vi.advanceTimersByTime(500));
      act(() => result.current.discard());
    }).not.toThrow();
    vi.unstubAllGlobals();
  });

  it("crypto indisponível → idempotencyKey via fallback", () => {
    vi.stubGlobal("crypto", undefined);
    const { result } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    expect(result.current.idempotencyKey).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
    );
    vi.unstubAllGlobals();
  });

  it("payload de versão diferente → ignorado", () => {
    localStorage.setItem(
      `exec-draft:${ALUNO}:${TREINO}`,
      JSON.stringify({
        v: 2,
        idempotencyKey: "k",
        updatedAt: Date.now(),
        execData: {},
        obsData: {},
        observacao: "",
        currentIndex: 0,
        treinoExercicioIds: [],
      }),
    );
    const { result } = renderHook(() => useExecucaoDraft(ALUNO, TREINO));
    expect(result.current.hasDraft).toBe(false);
  });
});
