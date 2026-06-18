"use client";
import { useEffect, useSyncExternalStore } from "react";
import {
  addFailureListener,
  addSuccessListener,
  drain,
  enqueue,
  getServerSnapshot,
  getSnapshot,
  subscribe,
  type RetryQueueFailure,
  type RetryQueueItem,
} from "@/lib/execucao/retryQueueStore";

export type { RetryQueueItem, RetryQueueFailure };

export interface UseExecucaoRetryQueueOptions {
  onSuccess?: (treinoId: string) => void;
  onError?: (failure: RetryQueueFailure) => void;
}

export interface UseExecucaoRetryQueueReturn {
  items: RetryQueueItem[];
  pendingCount: number;
  draining: boolean;
  enqueue: (item: Omit<RetryQueueItem, "enqueuedAt">) => void;
  drain: () => Promise<void>;
}

export function useExecucaoRetryQueue(
  opts?: UseExecucaoRetryQueueOptions,
): UseExecucaoRetryQueueReturn {
  const state = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);

  const onSuccess = opts?.onSuccess;
  const onError = opts?.onError;

  useEffect(() => {
    if (!onSuccess) return;
    return addSuccessListener(onSuccess);
  }, [onSuccess]);

  useEffect(() => {
    if (!onError) return;
    return addFailureListener(onError);
  }, [onError]);

  useEffect(() => {
    void drain();
  }, []);

  return {
    items: state.items,
    pendingCount: state.items.length,
    draining: state.draining,
    enqueue,
    drain,
  };
}
