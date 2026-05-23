import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { formatarData, formatarSeries, getWeekLabel, periodoParaDatas } from "@/lib/utils/formatting";
import type { SerieConfigResponse } from "@/types";

function makeSerie(overrides: Partial<SerieConfigResponse> = {}): SerieConfigResponse {
  return {
    serieConfigId: "s1",
    quantidade: 3,
    repeticoesMin: 10,
    repeticoesMax: null,
    descricao: null,
    carga: null,
    descanso: null,
    ordem: 1,
    ...overrides,
  };
}

// ─── formatarSeries ──────────────────────────────────────────────────────────

describe("formatarSeries", () => {
  it("array vazio → —", () => {
    expect(formatarSeries([])).toBe("—");
  });

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  it("null → —", () => expect(formatarSeries(null as any)).toBe("—"));

  it("1 série sem repeticoesMax → 3×10", () => {
    expect(formatarSeries([makeSerie()])).toBe("3×10");
  });

  it("1 série com repeticoesMax → 3×8–12", () => {
    expect(formatarSeries([makeSerie({ repeticoesMin: 8, repeticoesMax: 12 })])).toBe("3×8–12");
  });

  it("1 série com descricao → 3×10 (Pesado)", () => {
    expect(formatarSeries([makeSerie({ descricao: "Pesado" })])).toBe("3×10 (Pesado)");
  });

  it("1 série com repeticoesMax e descricao → 3×8–12 (Leve)", () => {
    expect(
      formatarSeries([makeSerie({ repeticoesMin: 8, repeticoesMax: 12, descricao: "Leve" })]),
    ).toBe("3×8–12 (Leve)");
  });

  it("múltiplas séries separadas por ' / '", () => {
    const series = [
      makeSerie({ quantidade: 3, repeticoesMin: 10, ordem: 1 }),
      makeSerie({ serieConfigId: "s2", quantidade: 4, repeticoesMin: 12, repeticoesMax: 15, ordem: 2 }),
    ];
    expect(formatarSeries(series)).toBe("3×10 / 4×12–15");
  });
});

// ─── formatarData ────────────────────────────────────────────────────────────

describe("formatarData", () => {
  it("ISO com time → DD/MM", () => {
    expect(formatarData("2025-03-15T00:00:00")).toBe("15/03");
  });

  it("ISO sem time → DD/MM", () => {
    expect(formatarData("2025-01-05")).toBe("05/01");
  });

  it("dezembro → 31/12", () => {
    expect(formatarData("2025-12-31T10:00:00Z")).toBe("31/12");
  });
});

// ─── getWeekLabel ────────────────────────────────────────────────────────────

describe("getWeekLabel", () => {
  // Semana de referência: 2025-03-03 a 2025-03-09 (seg → dom) → todos retornam "03/03"
  it.each([
    ["segunda", "2025-03-03T12:00:00"],
    ["terça",   "2025-03-04T12:00:00"],
    ["quarta",  "2025-03-05T12:00:00"],
    ["quinta",  "2025-03-06T12:00:00"],
    ["sexta",   "2025-03-07T12:00:00"],
    ["sábado",  "2025-03-08T12:00:00"],
    ["domingo", "2025-03-09T12:00:00"],
  ])("%s → segunda da semana (03/03)", (_, dateStr) => {
    expect(getWeekLabel(dateStr)).toBe("03/03");
  });
});

// ─── periodoParaDatas ────────────────────────────────────────────────────────

describe("periodoParaDatas", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    // Usar noon local para evitar cruzamento de dia ao converter para UTC
    vi.setSystemTime(new Date(2025, 2, 15, 12, 0, 0)); // 15/mar/2025 local noon
  });

  afterEach(() => vi.useRealTimers());

  it("7d → 7 dias atrás", () => {
    const { de, ate } = periodoParaDatas("7d");
    expect(ate).toMatch(/^2025-03-15/);
    expect(de).toMatch(/^2025-03-08/);
  });

  it("30d → 30 dias atrás", () => {
    const { de } = periodoParaDatas("30d");
    expect(de).toMatch(/^2025-02-13/);
  });

  it("60d → 60 dias atrás", () => {
    const { de } = periodoParaDatas("60d");
    expect(de).toMatch(/^2025-01-14/);
  });

  it("90d → 90 dias atrás", () => {
    const { de } = periodoParaDatas("90d");
    expect(de).toMatch(/^2024-12-15/);
  });

  it("6m → 6 meses atrás", () => {
    const { de } = periodoParaDatas("6m");
    expect(de).toMatch(/^2024-09-15/);
  });

  it("1a → 1 ano atrás", () => {
    const { de } = periodoParaDatas("1a");
    expect(de).toMatch(/^2024-03-15/);
  });

  it("período desconhecido → 10 anos atrás", () => {
    const { de } = periodoParaDatas("custom");
    expect(de).toMatch(/^2015-03-15/);
  });
});
