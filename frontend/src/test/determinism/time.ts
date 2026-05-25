import { vi } from "vitest";
import dayjs from "dayjs";
import "dayjs/locale/pt-br";

export const DETERMINISTIC_NOW_ISO = "2026-01-01T12:00:00.000Z";

/**
 * Congela o relógio dos testes em uma data fixa.
 * Necessário para qualquer codigo que use Date.now(), new Date() ou dayjs().
 */
export function freezeTime(iso: string = DETERMINISTIC_NOW_ISO): void {
  // Apenas Date e performance.now sao fakados por padrao.
  // Timers reais (setTimeout/setInterval) sao preservados para nao quebrar
  // waitFor() do Testing Library e user-event v14.
  // Testes que precisam controlar timers chamam vi.useFakeTimers() explicito.
  vi.useFakeTimers({
    now: new Date(iso),
    shouldAdvanceTime: false,
    toFake: ["Date", "performance"],
  });
  dayjs.locale("pt-br");
}

/**
 * Avanca o relogio fake em N milissegundos.
 */
export function advanceTime(ms: number): void {
  vi.advanceTimersByTime(ms);
}

/**
 * Restaura o relogio real (use no afterEach).
 */
export function restoreTime(): void {
  vi.useRealTimers();
}
