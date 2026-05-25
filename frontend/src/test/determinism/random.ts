import seedrandom from "seedrandom";

export const DETERMINISTIC_SEED = 42;

let originalRandom: typeof Math.random | null = null;

/**
 * Substitui Math.random por gerador seedado.
 * Tornar testes 100% reproduziveis quando codigo usa aleatoriedade.
 */
export function seedRandom(seed: number = DETERMINISTIC_SEED): void {
  if (originalRandom === null) {
    originalRandom = Math.random;
  }
  const prng = seedrandom(String(seed));
  Math.random = () => prng();
}

/**
 * Restaura Math.random original.
 */
export function restoreRandom(): void {
  if (originalRandom !== null) {
    Math.random = originalRandom;
    originalRandom = null;
  }
}
