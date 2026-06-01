import { freezeTime, restoreTime, DETERMINISTIC_NOW_ISO } from "./time";
import { seedRandom, restoreRandom, DETERMINISTIC_SEED } from "./random";
import { deterministicUuid, resetUuid } from "./uuid";
import { forceReducedMotion } from "./motion";

export interface DeterminismOptions {
  /**
   * ISO string para congelar Date.now(). Opt-in.
   * Default: nao congela (tempo real). Use freezeTime() explicito no teste
   * quando precisar de Date deterministico (ex: snapshots, formatacao, dayjs).
   *
   * Default opt-in porque modulos com `Date.now()` em top-level (FUTURE/PAST)
   * sao avaliados antes do beforeEach e ficariam dessincronizados com fake clock.
   */
  time?: string | false;
  seed?: number;
  motion?: boolean;
}

/**
 * Instala controles deterministicos para um teste:
 * - Math.random seedado (sempre)
 * - crypto.randomUUID com contador (sempre)
 * - prefers-reduced-motion: reduce (default true)
 * - Tempo congelado (opt-in via options.time)
 *
 * Chamar em beforeEach. Restaurar em afterEach via uninstallDeterminism().
 */
export function installDeterminism(options: DeterminismOptions = {}): void {
  if (typeof options.time === "string") {
    freezeTime(options.time);
  }
  seedRandom(options.seed ?? DETERMINISTIC_SEED);
  deterministicUuid();
  if (options.motion !== false && typeof window !== "undefined") {
    forceReducedMotion();
  }
}

export function uninstallDeterminism(): void {
  restoreTime();
  restoreRandom();
  resetUuid();
}

export {
  freezeTime,
  restoreTime,
  DETERMINISTIC_NOW_ISO,
} from "./time";
export { seedRandom, restoreRandom, DETERMINISTIC_SEED } from "./random";
export { deterministicUuid, resetUuid } from "./uuid";
export { forceReducedMotion } from "./motion";
