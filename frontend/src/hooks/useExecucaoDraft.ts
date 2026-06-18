"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import type { TreinoExercicioResponse } from "@/types";
import { initExecData, type SetState } from "@/lib/execucao/execData";

const DRAFT_VERSION = 1;
const TTL_MS = 48 * 60 * 60 * 1000;
const AUTOSAVE_DEBOUNCE_MS = 500;

export interface DraftState {
  execData: Record<string, SetState[]>;
  obsData: Record<string, string>;
  observacao: string;
  currentIndex: number;
}

interface DraftPayload extends DraftState {
  v: typeof DRAFT_VERSION;
  idempotencyKey: string;
  treinoExercicioIds: string[];
  updatedAt: number;
}

export interface ReconcileResult extends DraftState {
  reconciled: boolean;
}

export interface UseExecucaoDraftReturn {
  idempotencyKey: string;
  hasDraft: boolean;
  draftMeta: { updatedAt: number } | null;
  save: (state: DraftState) => void;
  restore: () => DraftState | null;
  reconcile: (exercicios: TreinoExercicioResponse[]) => ReconcileResult | null;
  discard: () => void;
}

function storageKey(alunoId: string, treinoId: string): string {
  return `exec-draft:${alunoId}:${treinoId}`;
}

function genId(): string {
  try {
    if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
      return crypto.randomUUID();
    }
  } catch {
    // crypto indisponível (contexto inseguro) → cai no fallback
  }
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

function safeGet(key: string): string | null {
  try {
    if (typeof localStorage === "undefined") return null;
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function safeSet(key: string, value: string): void {
  try {
    if (typeof localStorage === "undefined") return;
    localStorage.setItem(key, value);
  } catch {
    // quota cheia / Safari privado → no-op (EXOFF-06)
  }
}

function safeRemove(key: string): void {
  try {
    if (typeof localStorage === "undefined") return;
    localStorage.removeItem(key);
  } catch {
    // no-op
  }
}

function readPayload(key: string): DraftPayload | null {
  const raw = safeGet(key);
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as DraftPayload;
    if (parsed?.v !== DRAFT_VERSION || typeof parsed.idempotencyKey !== "string") return null;
    if (Date.now() - parsed.updatedAt > TTL_MS) return null;
    return parsed;
  } catch {
    return null;
  }
}

export function useExecucaoDraft(alunoId: string, treinoId: string): UseExecucaoDraftReturn {
  const key = storageKey(alunoId, treinoId);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const [idempotencyKey, setIdempotencyKey] = useState<string>(() => {
    const existing = readPayload(key);
    return existing?.idempotencyKey ?? genId();
  });
  const [hasDraft, setHasDraft] = useState<boolean>(() => readPayload(key) !== null);
  const [draftMeta, setDraftMeta] = useState<{ updatedAt: number } | null>(() => {
    const existing = readPayload(key);
    return existing ? { updatedAt: existing.updatedAt } : null;
  });

  useEffect(() => {
    const expired = safeGet(key) !== null && readPayload(key) === null;
    if (expired) safeRemove(key);
  }, [key]);

  const discard = useCallback(() => {
    if (timer.current) {
      clearTimeout(timer.current);
      timer.current = null;
    }
    safeRemove(key);
    setHasDraft(false);
    setDraftMeta(null);
    setIdempotencyKey(genId());
  }, [key]);

  const save = useCallback(
    (state: DraftState) => {
      if (timer.current) clearTimeout(timer.current);
      timer.current = setTimeout(() => {
        const updatedAt = Date.now();
        const payload: DraftPayload = {
          v: DRAFT_VERSION,
          idempotencyKey,
          treinoExercicioIds: Object.keys(state.execData),
          updatedAt,
          ...state,
        };
        safeSet(key, JSON.stringify(payload));
        setHasDraft(true);
        setDraftMeta({ updatedAt });
      }, AUTOSAVE_DEBOUNCE_MS);
    },
    [key, idempotencyKey],
  );

  const restore = useCallback((): DraftState | null => {
    const payload = readPayload(key);
    if (!payload) return null;
    return {
      execData: payload.execData,
      obsData: payload.obsData,
      observacao: payload.observacao,
      currentIndex: payload.currentIndex,
    };
  }, [key]);

  const reconcile = useCallback(
    (exercicios: TreinoExercicioResponse[]): ReconcileResult | null => {
      const payload = readPayload(key);
      if (!payload) return null;

      const currentIds = exercicios.map((e) => e.treinoExercicioId);
      const execData: Record<string, SetState[]> = {};
      let reconciled = false;

      for (const ex of exercicios) {
        const fromDraft = payload.execData[ex.treinoExercicioId];
        if (fromDraft) {
          execData[ex.treinoExercicioId] = fromDraft;
        } else {
          execData[ex.treinoExercicioId] = initExecData([ex])[ex.treinoExercicioId];
          reconciled = true;
        }
      }
      for (const id of payload.treinoExercicioIds) {
        if (!currentIds.includes(id)) reconciled = true;
      }

      const obsData: Record<string, string> = {};
      for (const id of currentIds) {
        if (payload.obsData?.[id] != null) obsData[id] = payload.obsData[id];
      }

      const maxIndex = Math.max(0, exercicios.length - 1);
      return {
        execData,
        obsData,
        observacao: payload.observacao,
        currentIndex: Math.min(payload.currentIndex, maxIndex),
        reconciled,
      };
    },
    [key],
  );

  useEffect(() => {
    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
  }, []);

  return { idempotencyKey, hasDraft, draftMeta, save, restore, reconcile, discard };
}
