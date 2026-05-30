/**
 * Property-based tests para funcoes puras de formatacao.
 *
 * Cobertura:
 * - formatarSeries: invariantes de output (sempre string, "—" para vazio)
 * - formatarData: extrai DD/MM de qualquer ISO valido
 * - getWeekLabel: produz formato DD/MM
 */
import { describe, expect } from "vitest";
import { fc, test } from "@fast-check/vitest";
import { formatarSeries, formatarData, getWeekLabel } from "./formatting";
import type { SerieConfigResponse } from "@/types";

describe("formatarSeries", () => {
  test.prop([fc.constant([])])("array vazio sempre retorna '—'", (empty) => {
    expect(formatarSeries(empty as unknown as SerieConfigResponse[])).toBe("—");
  });

  test.prop([
    fc.array(
      fc.record({
        serieConfigId: fc.string(),
        quantidade: fc.integer({ min: 1, max: 10 }),
        repeticoesMin: fc.integer({ min: 1, max: 30 }),
        repeticoesMax: fc.option(fc.integer({ min: 1, max: 30 }), { nil: null }),
        descricao: fc.option(fc.string({ maxLength: 50 }), { nil: null }),
        carga: fc.option(fc.float({ min: 0, max: 200 }), { nil: null }),
        descanso: fc.option(fc.integer({ min: 0, max: 600 }), { nil: null }),
        ordem: fc.integer({ min: 1, max: 10 }),
      }) satisfies fc.Arbitrary<import("@/types").SerieConfigResponse>,
      { minLength: 1, maxLength: 5 },
    ),
  ])("array nao-vazio sempre retorna string nao-vazia", (series) => {
    const result = formatarSeries(series as unknown as SerieConfigResponse[]);
    expect(typeof result).toBe("string");
    expect(result.length).toBeGreaterThan(0);
    expect(result).not.toBe("—");
  });

  test.prop([
    fc.array(
      fc.record({
        serieConfigId: fc.string(),
        treinoExercicioId: fc.string(),
        quantidade: fc.integer({ min: 1, max: 10 }),
        repeticoesMin: fc.integer({ min: 1, max: 30 }),
        repeticoesMax: fc.option(fc.integer({ min: 1, max: 30 }), { nil: null }),
        descricao: fc.constant(null),
        ordem: fc.integer({ min: 1, max: 10 }),
      }),
      { minLength: 2, maxLength: 5 },
    ),
  ])("N series sem descricao gera N-1 separadores ' / '", (series) => {
    const result = formatarSeries(series as unknown as SerieConfigResponse[]);
    const separatorCount = (result.match(/ \/ /g) ?? []).length;
    expect(separatorCount).toBe(series.length - 1);
  });
});

describe("formatarData", () => {
  test.prop([
    fc.date({ min: new Date("2000-01-01"), max: new Date("2099-12-31"), noInvalidDate: true }),
  ])("retorna formato DD/MM para qualquer Date valido", (date) => {
    const iso = date.toISOString();
    const result = formatarData(iso);
    expect(result).toMatch(/^\d{2}\/\d{2}$/);
  });

  test.prop([
    fc.date({ min: new Date("2000-01-01"), max: new Date("2099-12-31"), noInvalidDate: true }),
  ])("DD eh igual ao dia UTC do Date original", (date) => {
    const iso = date.toISOString();
    const result = formatarData(iso);
    const dia = String(date.getUTCDate()).padStart(2, "0");
    expect(result.slice(0, 2)).toBe(dia);
  });

  test.prop([
    fc.date({ min: new Date("2000-01-01"), max: new Date("2099-12-31"), noInvalidDate: true }),
  ])("MM eh igual ao mes UTC (1-indexed)", (date) => {
    const iso = date.toISOString();
    const result = formatarData(iso);
    const mes = String(date.getUTCMonth() + 1).padStart(2, "0");
    expect(result.slice(3, 5)).toBe(mes);
  });
});

describe("getWeekLabel", () => {
  test.prop([
    fc.date({ min: new Date("2000-01-01"), max: new Date("2099-12-31"), noInvalidDate: true }),
  ])("sempre retorna formato DD/MM", (date) => {
    const dateStr = date.toISOString();
    const result = getWeekLabel(dateStr);
    expect(result).toMatch(/^\d{2}\/\d{2}$/);
  });
});
