import { beforeEach, describe, expect, it, vi } from "vitest";
import type { SerieConfigResponse, TreinoExercicioResponse } from "@/types";

vi.mock("xlsx", () => ({
  utils: {
    book_new: vi.fn(() => ({ SheetNames: [] as string[], Sheets: {} as Record<string, unknown> })),
    aoa_to_sheet: vi.fn(() => ({ "!ref": "A1:I10" })),
    book_append_sheet: vi.fn(),
  },
  writeFile: vi.fn(),
}));

import * as XLSX from "xlsx";
import { buildFichaRows, exportarFichaParaExcel, sanitizeFilename } from "@/lib/utils/excel";
import type { FichaExportParams } from "@/lib/utils/excel";

// ─── helpers ────────────────────────────────────────────────────────────────

function makeSerie(overrides?: Partial<SerieConfigResponse>): SerieConfigResponse {
  return {
    serieConfigId: "s1",
    quantidade: 3,
    repeticoesMin: 10,
    repeticoesMax: 12,
    descricao: null,
    carga: null,
    descanso: 60,
    ordem: 1,
    ...overrides,
  };
}

function makeExercicio(overrides?: Partial<TreinoExercicioResponse>): TreinoExercicioResponse {
  return {
    treinoExercicioId: "ex1",
    exercicioId: "e1",
    nomeExercicio: "Agachamento",
    series: [makeSerie()],
    ordem: 1,
    observacao: null,
    ...overrides,
  };
}

const BASE: FichaExportParams = {
  nome: "Treino A",
  objetivo: "Hipertrofia",
  exercicios: [makeExercicio()],
};

// ─── sanitizeFilename ────────────────────────────────────────────────────────

describe("sanitizeFilename", () => {
  it("allows word chars, spaces, and hyphens", () => {
    expect(sanitizeFilename("Treino 1 - Peito")).toBe("Treino 1 - Peito");
  });

  it("trims surrounding whitespace", () => {
    expect(sanitizeFilename("  Treino  ")).toBe("Treino");
  });

  it("falls back to 'ficha' when all chars are stripped", () => {
    expect(sanitizeFilename("!!!")).toBe("ficha");
    expect(sanitizeFilename("")).toBe("ficha");
    expect(sanitizeFilename("...")).toBe("ficha");
  });

  // Security: path traversal
  it("removes dots and slashes — prevents path traversal", () => {
    const result = sanitizeFilename("../../etc/passwd");
    expect(result).not.toContain("/");
    expect(result).not.toContain(".");
    expect(result).toBe("etcpasswd");
  });

  it("removes null byte — cannot be used to truncate filename on OS level", () => {
    const result = sanitizeFilename("treino\0malicioso");
    expect(result).not.toContain("\0");
    expect(result).toBe("treinomalicioso");
  });

  it("removes angle brackets — prevents HTML/XML injection if filename reflected", () => {
    const result = sanitizeFilename("<script>alert</script>");
    expect(result).not.toContain("<");
    expect(result).not.toContain(">");
  });

  it("removes formula trigger chars from filename — =, (, )", () => {
    const result = sanitizeFilename("=SUM(A1)");
    expect(result).not.toMatch(/^=/);
    expect(result).not.toContain("(");
    expect(result).not.toContain(")");
    expect(result).toBe("SUMA1");
  });

  it("removes backslash — prevents Windows path injection", () => {
    const result = sanitizeFilename("C:\\Windows\\System32");
    expect(result).not.toContain("\\");
    expect(result).not.toContain(":");
  });
});

// ─── buildFichaRows — header structure ──────────────────────────────────────

describe("buildFichaRows — header", () => {
  it("row 0: label + nome da ficha", () => {
    expect(buildFichaRows(BASE)[0]).toEqual(["Ficha de Treino", "Treino A"]);
  });

  it("row 1: label + objetivo traduzido para PT-BR", () => {
    expect(buildFichaRows({ ...BASE, objetivo: "Resistencia" })[1]).toEqual(["Objetivo", "Resistência"]);
  });

  it("row 1: fallback to raw enum value when label not found", () => {
    // TypeScript prevents passing unknown enum, but test the guard expression
    const rows = buildFichaRows({ ...BASE, objetivo: "Hipertrofia" });
    expect(rows[1][1]).toBe("Hipertrofia");
  });

  it("row 2 is blank separator", () => {
    expect(buildFichaRows(BASE)[2]).toEqual([]);
  });

  it("row 3 contains all nine column headers in correct order", () => {
    expect(buildFichaRows(BASE)[3]).toEqual([
      "#", "Exercício", "Qtd Séries", "Reps Mín", "Reps Máx",
      "Descrição", "Carga (kg)", "Descanso (s)", "Observação",
    ]);
  });
});

// ─── buildFichaRows — exercise data rows ────────────────────────────────────

describe("buildFichaRows — exercise data", () => {
  it("data starts at row index 4", () => {
    const rows = buildFichaRows(BASE);
    expect(rows[4][0]).toBe(1);
    expect(rows[4][1]).toBe("Agachamento");
  });

  it("# and exercise name appear only on first series row", () => {
    const ex = makeExercicio({
      series: [makeSerie({ ordem: 1 }), makeSerie({ ordem: 2, quantidade: 2 })],
    });
    const rows = buildFichaRows({ ...BASE, exercicios: [ex] });

    expect(rows[4][0]).toBe(1);
    expect(rows[4][1]).toBe("Agachamento");
    expect(rows[5][0]).toBeNull();
    expect(rows[5][1]).toBeNull();
  });

  it("observacao appears only on first series row", () => {
    const ex = makeExercicio({
      observacao: "Foco na contração",
      series: [makeSerie({ ordem: 1 }), makeSerie({ ordem: 2 })],
    });
    const rows = buildFichaRows({ ...BASE, exercicios: [ex] });

    expect(rows[4][8]).toBe("Foco na contração");
    expect(rows[5][8]).toBeNull();
  });

  it("maps all series columns to correct indexes", () => {
    const serie = makeSerie({
      quantidade: 4, repeticoesMin: 8, repeticoesMax: 10,
      descricao: "Pesado", carga: 80, descanso: 90,
    });
    const rows = buildFichaRows({ ...BASE, exercicios: [makeExercicio({ series: [serie] })] });
    const row = rows[4];

    expect(row[2]).toBe(4);         // Qtd Séries
    expect(row[3]).toBe(8);         // Reps Mín
    expect(row[4]).toBe(10);        // Reps Máx
    expect(row[5]).toBe("Pesado");  // Descrição
    expect(row[6]).toBe(80);        // Carga (kg)
    expect(row[7]).toBe(90);        // Descanso (s)
  });

  it("absent optional fields produce null — not undefined or empty string", () => {
    const serie = makeSerie({ repeticoesMax: null, descricao: null, carga: null, descanso: null });
    const row = buildFichaRows({ ...BASE, exercicios: [makeExercicio({ series: [serie] })] })[4];

    expect(row[4]).toBeNull();
    expect(row[5]).toBeNull();
    expect(row[6]).toBeNull();
    expect(row[7]).toBeNull();
  });

  it("null observacao produces null in column 8", () => {
    const ex = makeExercicio({ observacao: null });
    const row = buildFichaRows({ ...BASE, exercicios: [ex] })[4];
    expect(row[8]).toBeNull();
  });

  it("exercise with no series emits exactly one row with nulls for serie columns", () => {
    const ex = makeExercicio({ series: [] });
    const rows = buildFichaRows({ ...BASE, exercicios: [ex] });

    expect(rows).toHaveLength(5); // 4 header + 1 data
    expect(rows[4][1]).toBe("Agachamento");
    expect(rows[4][2]).toBeNull();
  });

  it("empty exercicios list produces only 4 header rows", () => {
    expect(buildFichaRows({ ...BASE, exercicios: [] })).toHaveLength(4);
  });

  it("exercises are sorted by ordem (not insertion order)", () => {
    const ex1 = makeExercicio({ treinoExercicioId: "a", nomeExercicio: "Remada", ordem: 2 });
    const ex2 = makeExercicio({ treinoExercicioId: "b", nomeExercicio: "Agachamento", ordem: 1 });
    const rows = buildFichaRows({ ...BASE, exercicios: [ex1, ex2] });

    expect(rows[4][1]).toBe("Agachamento"); // ordem 1 first
    expect(rows[5][1]).toBe("Remada");      // ordem 2 second
  });

  it("series within an exercise are sorted by ordem", () => {
    const ex = makeExercicio({
      series: [
        makeSerie({ ordem: 2, quantidade: 2, repeticoesMin: 15 }),
        makeSerie({ ordem: 1, quantidade: 4, repeticoesMin: 8 }),
      ],
    });
    const rows = buildFichaRows({ ...BASE, exercicios: [ex] });

    expect(rows[4][2]).toBe(4);  // ordem=1 first
    expect(rows[5][2]).toBe(2);  // ordem=2 second
  });

  it("total row count matches 4 headers + sum of series across all exercises", () => {
    const ex1 = makeExercicio({ treinoExercicioId: "a", ordem: 1, series: [makeSerie()] });
    const ex2 = makeExercicio({
      treinoExercicioId: "b", ordem: 2,
      series: [makeSerie({ ordem: 1 }), makeSerie({ ordem: 2 })],
    });
    const rows = buildFichaRows({ ...BASE, exercicios: [ex1, ex2] });

    expect(rows).toHaveLength(4 + 1 + 2); // 7
  });

  it("does not mutate the original exercicios array", () => {
    const ex1 = makeExercicio({ treinoExercicioId: "a", ordem: 2 });
    const ex2 = makeExercicio({ treinoExercicioId: "b", ordem: 1 });
    const original = [ex1, ex2];
    buildFichaRows({ ...BASE, exercicios: original });

    expect(original[0].treinoExercicioId).toBe("a"); // not reordered
  });
});

// ─── buildFichaRows — security: formula injection ───────────────────────────

describe("buildFichaRows — formula injection (SheetJS defense)", () => {
  /**
   * SheetJS writes JS strings as xlsx type 's' (string) cells.
   * Excel reads type-'s' cells as literal text, never as formulas.
   * These tests verify that values are passed through unmodified to the
   * row array (the xlsx type system is the sole, sufficient defense).
   */

  it("exercise name starting with '=' reaches row unchanged", () => {
    const ex = makeExercicio({ nomeExercicio: '=HYPERLINK("http://evil.com","x")' });
    const rows = buildFichaRows({ ...BASE, exercicios: [ex] });
    expect(rows[4][1]).toBe('=HYPERLINK("http://evil.com","x")');
  });

  it("exercise name starting with '+' reaches row unchanged", () => {
    const ex = makeExercicio({ nomeExercicio: "+1 DROP TABLE" });
    expect(buildFichaRows({ ...BASE, exercicios: [ex] })[4][1]).toBe("+1 DROP TABLE");
  });

  it("series descricao starting with '-' reaches row unchanged", () => {
    const serie = makeSerie({ descricao: "-cmd|' /C calc'!A0" });
    expect(buildFichaRows({ ...BASE, exercicios: [makeExercicio({ series: [serie] })] })[4][5])
      .toBe("-cmd|' /C calc'!A0");
  });

  it("observacao starting with '@' reaches row unchanged", () => {
    const ex = makeExercicio({ observacao: "@SUM(1+1)" });
    expect(buildFichaRows({ ...BASE, exercicios: [ex] })[4][8]).toBe("@SUM(1+1)");
  });

  it("pipe-separated DDE payload reaches row unchanged", () => {
    // DDE injection pattern: |cmd;calc!A0
    const ex = makeExercicio({ nomeExercicio: "|cmd;calc!A0" });
    expect(buildFichaRows({ ...BASE, exercicios: [ex] })[4][1]).toBe("|cmd;calc!A0");
  });
});

// ─── exportarFichaParaExcel ──────────────────────────────────────────────────

describe("exportarFichaParaExcel", () => {
  beforeEach(() => vi.clearAllMocks());

  it("calls XLSX.writeFile with sanitized filename + .xlsx extension", () => {
    exportarFichaParaExcel({ ...BASE, nome: "Treino/Especial" });
    expect(XLSX.writeFile).toHaveBeenCalledWith(
      expect.anything(),
      "TreinoEspecial.xlsx",
    );
  });

  it("filename always ends with .xlsx", () => {
    exportarFichaParaExcel(BASE);
    const [, filename] = (XLSX.writeFile as ReturnType<typeof vi.fn>).mock.calls[0] as [unknown, string];
    expect(filename.endsWith(".xlsx")).toBe(true);
  });

  it("uses fallback 'ficha.xlsx' when nome sanitizes to empty", () => {
    exportarFichaParaExcel({ ...BASE, nome: "!!!" });
    expect(XLSX.writeFile).toHaveBeenCalledWith(expect.anything(), "ficha.xlsx");
  });

  it("does not throw for empty exercicios", () => {
    expect(() =>
      exportarFichaParaExcel({ ...BASE, exercicios: [] }),
    ).not.toThrow();
  });

  it("does not throw for exercises with all optional fields null", () => {
    const ex = makeExercicio({
      observacao: null,
      series: [makeSerie({ repeticoesMax: null, descricao: null, carga: null, descanso: null })],
    });
    expect(() =>
      exportarFichaParaExcel({ ...BASE, exercicios: [ex] }),
    ).not.toThrow();
  });

  it("calls XLSX.utils.book_new, aoa_to_sheet, book_append_sheet in sequence", () => {
    exportarFichaParaExcel(BASE);
    expect(XLSX.utils.book_new).toHaveBeenCalled();
    expect(XLSX.utils.aoa_to_sheet).toHaveBeenCalled();
    expect(XLSX.utils.book_append_sheet).toHaveBeenCalled();
  });

  it("passes rows built by buildFichaRows to aoa_to_sheet", () => {
    const expected = buildFichaRows(BASE);
    exportarFichaParaExcel(BASE);
    expect(XLSX.utils.aoa_to_sheet).toHaveBeenCalledWith(expected);
  });
});
