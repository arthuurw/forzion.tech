import { alunoApi, type CriarExecucaoData } from "@/lib/api/aluno";
import { extractApiErrorInfo } from "@/lib/api/extractApiError";
import { safeGet, safeRemove, safeSet } from "@/lib/storage/safeStorage";

const QUEUE_KEY = "exec-queue";
const BACKOFF_MS = [5000, 15000, 45000];
const MAX_ATTEMPTS = 5;

export interface RetryQueueItem {
  idempotencyKey: string;
  payload: CriarExecucaoData;
  alunoId: string;
  treinoId: string;
  enqueuedAt: number;
  attempts?: number;
  lastError?: string;
}

export interface RetryQueueFailure {
  item: RetryQueueItem;
  status: number | null;
  message: string | null;
  permanent: boolean;
}

export interface RetryQueueState {
  items: RetryQueueItem[];
  draining: boolean;
}

const SERVER_STATE: RetryQueueState = { items: [], draining: false };

type SuccessListener = (treinoId: string) => void;
type FailureListener = (failure: RetryQueueFailure) => void;

const subscribers = new Set<() => void>();
const successListeners = new Set<SuccessListener>();
const failureListeners = new Set<FailureListener>();

let state: RetryQueueState = { items: readRaw(), draining: false };
let draining = false;
let backoffTimer: ReturnType<typeof setTimeout> | null = null;

function readRaw(): RetryQueueItem[] {
  const raw = safeGet(QUEUE_KEY);
  if (!raw) return [];
  try {
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? (parsed as RetryQueueItem[]) : [];
  } catch {
    return [];
  }
}

function emit(): void {
  subscribers.forEach((fn) => fn());
}

function setItems(next: RetryQueueItem[]): void {
  if (next.length === 0) safeRemove(QUEUE_KEY);
  else safeSet(QUEUE_KEY, JSON.stringify(next));
  state = { items: next, draining };
  emit();
}

function setDraining(value: boolean): void {
  draining = value;
  state = { items: state.items, draining };
  emit();
}

function notifySuccess(treinoId: string): void {
  successListeners.forEach((fn) => fn(treinoId));
}

function notifyFailure(failure: RetryQueueFailure): void {
  failureListeners.forEach((fn) => fn(failure));
}

function clearBackoff(): void {
  if (backoffTimer !== null) {
    clearTimeout(backoffTimer);
    backoffTimer = null;
  }
}

function scheduleBackoff(queue: RetryQueueItem[]): void {
  if (backoffTimer !== null || queue.length === 0) return;
  const attempts = queue[0].attempts ?? 1;
  const delay = BACKOFF_MS[Math.min(attempts - 1, BACKOFF_MS.length - 1)];
  backoffTimer = setTimeout(() => {
    backoffTimer = null;
    void drain();
  }, delay);
}

export function subscribe(fn: () => void): () => void {
  if (subscribers.size === 0) state = { items: readRaw(), draining };
  subscribers.add(fn);
  return () => {
    subscribers.delete(fn);
  };
}

export function getSnapshot(): RetryQueueState {
  return state;
}

export function getServerSnapshot(): RetryQueueState {
  return SERVER_STATE;
}

export function addSuccessListener(fn: SuccessListener): () => void {
  successListeners.add(fn);
  return () => {
    successListeners.delete(fn);
  };
}

export function addFailureListener(fn: FailureListener): () => void {
  failureListeners.add(fn);
  return () => {
    failureListeners.delete(fn);
  };
}

export function enqueue(item: Omit<RetryQueueItem, "enqueuedAt">): void {
  const next = [...readRaw(), { ...item, enqueuedAt: Date.now() }];
  setItems(next);
  scheduleBackoff(next);
}

export async function drain(): Promise<void> {
  if (draining) return;
  const queue = readRaw();
  if (queue.length === 0) return;
  draining = true;
  setDraining(true);
  try {
    const remaining: RetryQueueItem[] = [];
    let stopped = false;
    let scheduleRetry = false;
    for (const item of queue) {
      if (stopped) {
        remaining.push(item);
        continue;
      }
      try {
        await alunoApi.criarExecucao(item.payload, { idempotencyKey: item.idempotencyKey });
        notifySuccess(item.treinoId);
      } catch (err) {
        const { status, message } = extractApiErrorInfo(err);
        const transient = status === null || status >= 500 || status === 429;
        if (!transient) {
          notifyFailure({ item, status, message, permanent: true });
          continue;
        }
        const attempts = (item.attempts ?? 0) + 1;
        if (attempts >= MAX_ATTEMPTS) {
          notifyFailure({ item, status, message, permanent: false });
          continue;
        }
        remaining.push({ ...item, attempts, lastError: message ?? `Erro ${status ?? "rede"}` });
        stopped = true;
        scheduleRetry = true;
      }
    }
    setItems(remaining);
    if (scheduleRetry) scheduleBackoff(remaining);
  } finally {
    draining = false;
    setDraining(false);
  }
}

function onOnline(): void {
  clearBackoff();
  void drain();
}

if (typeof window !== "undefined") {
  window.addEventListener("online", onOnline);
  window.addEventListener("storage", (e) => {
    if (e.key !== QUEUE_KEY) return;
    state = { items: readRaw(), draining };
    emit();
  });
}

export function __resetRetryQueueStore(): void {
  clearBackoff();
  subscribers.clear();
  successListeners.clear();
  failureListeners.clear();
  draining = false;
  state = { items: readRaw(), draining: false };
}
