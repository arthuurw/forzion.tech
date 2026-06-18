"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import { alunoApi, type CriarExecucaoData } from "@/lib/api/aluno";
import { extractApiErrorInfo } from "@/lib/api/extractApiError";

const QUEUE_KEY = "exec-queue";

export interface RetryQueueItem {
  idempotencyKey: string;
  payload: CriarExecucaoData;
  alunoId: string;
  treinoId: string;
  enqueuedAt: number;
  lastError?: string;
}

export interface UseExecucaoRetryQueueOptions {
  onSuccess?: (treinoId: string) => void;
}

export interface UseExecucaoRetryQueueReturn {
  items: RetryQueueItem[];
  pendingCount: number;
  draining: boolean;
  enqueue: (item: Omit<RetryQueueItem, "enqueuedAt">) => void;
  drain: () => Promise<void>;
}

function read(): RetryQueueItem[] {
  try {
    if (typeof localStorage === "undefined") return [];
    const raw = localStorage.getItem(QUEUE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? (parsed as RetryQueueItem[]) : [];
  } catch {
    return [];
  }
}

function write(items: RetryQueueItem[]): void {
  try {
    if (typeof localStorage === "undefined") return;
    if (items.length === 0) localStorage.removeItem(QUEUE_KEY);
    else localStorage.setItem(QUEUE_KEY, JSON.stringify(items));
  } catch {
    // quota / indisponível → no-op
  }
}

export function useExecucaoRetryQueue(
  opts?: UseExecucaoRetryQueueOptions,
): UseExecucaoRetryQueueReturn {
  const [items, setItems] = useState<RetryQueueItem[]>(() => read());
  const [draining, setDraining] = useState(false);
  const drainingRef = useRef(false);

  const onSuccessRef = useRef(opts?.onSuccess);
  useEffect(() => {
    onSuccessRef.current = opts?.onSuccess;
  });

  const enqueue = useCallback((item: Omit<RetryQueueItem, "enqueuedAt">) => {
    const next = [...read(), { ...item, enqueuedAt: Date.now() }];
    write(next);
    setItems(next);
  }, []);

  const drain = useCallback(async () => {
    if (drainingRef.current) return;
    const queue = read();
    if (queue.length === 0) return;
    drainingRef.current = true;
    setDraining(true);
    try {
      const remaining: RetryQueueItem[] = [];
      let stopped = false;
      for (const item of queue) {
        if (stopped) {
          remaining.push(item);
          continue;
        }
        try {
          await alunoApi.criarExecucao(item.payload, { idempotencyKey: item.idempotencyKey });
          onSuccessRef.current?.(item.treinoId);
        } catch (err) {
          const { status, message } = extractApiErrorInfo(err);
          const transient = status === null || status >= 500;
          if (transient) {
            remaining.push(item);
            stopped = true;
          } else {
            remaining.push({ ...item, lastError: message ?? `Erro ${status}` });
          }
        }
      }
      write(remaining);
      setItems(remaining);
    } finally {
      drainingRef.current = false;
      setDraining(false);
    }
  }, []);

  const drainRef = useRef(drain);
  useEffect(() => {
    drainRef.current = drain;
  });

  useEffect(() => {
    void drainRef.current();
    const onOnline = () => void drainRef.current();
    if (typeof window !== "undefined") {
      window.addEventListener("online", onOnline);
      return () => window.removeEventListener("online", onOnline);
    }
  }, []);

  return { items, pendingCount: items.length, draining, enqueue, drain };
}
