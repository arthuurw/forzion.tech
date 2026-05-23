import { vi } from "vitest";

let counter = 0;

function nextUuid(): `${string}-${string}-${string}-${string}-${string}` {
  counter += 1;
  const hex = counter.toString(16).padStart(12, "0");
  return `00000000-0000-4000-8000-${hex}`;
}

/**
 * Substitui crypto.randomUUID por contador deterministico.
 * Garante UUIDs reproduziveis entre runs.
 */
export function deterministicUuid(): void {
  counter = 0;
  if (typeof globalThis.crypto === "undefined") {
    (globalThis as { crypto?: Crypto }).crypto = {} as Crypto;
  }
  vi.spyOn(globalThis.crypto, "randomUUID").mockImplementation(nextUuid);
}

/**
 * Reseta contador.
 */
export function resetUuid(): void {
  counter = 0;
}
