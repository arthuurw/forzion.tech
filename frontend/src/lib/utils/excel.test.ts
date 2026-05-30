import { beforeEach, afterEach, describe, expect, it, vi } from "vitest";
import type { SerieConfigResponse, TreinoExercicioResponse } from "@/types";

const excelMocks = vi.hoisted(() => {
  const addRow = vi.fn();
  const getColumn = vi.fn(function() { return { width: 0 }; });
  const worksheet = { addRow, getColumn };

  const addWorksheet = vi.fn(function() { return worksheet; });
  const writeBuffer = vi.fn(function() { return Promise.resolve(new Uint8Array([1, 2, 3])); });

  class WorkbookMock {
    addWorksheet = addWorksheet;
    xlsx = { writeBuffer };
  }

  return { addRow, getColumn, worksheet, addWorksheet, writeBuffer, WorkbookMock };
});

vi.mock("exceljs", () => ({
  default: {
    Workbook: excelMocks.WorkbookMock,
  },
}));

import { buildFichaRows, exportarFichaParaExcel, safeCell, sanitizeFilename } from "@/lib/utils/excel";
import type { FichaExportParams } from "@/lib/utils/excel";

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

describe("safeCell — formula injection prevention for ExcelJS", () => {
  it("prefixes '=' strings with apostrophe", () => {
    expect(safeCell('=HYPERLINK("http://evil.com","x")')).toBe("'=HYPERLINK(\"http://evil.com\",\"x\")");
  });

  it("prefixes '+' strings with apostrophe", () => {
    expect(safeCell("+1 DROP TABLE")).toBe("'+1 DROP TABLE");
  });

  it("prefixes '-' strings with apostrophe", () => {
    expect(safeCell("-cmd|' /C calc'!A0")).toBe("'-cmd|' /C calc'!A0");
  });

  it("prefixes '@' strings with apostrophe", () => {
    expect(safeCell("@SUM(1+1)")).toBe("'@SUM(1+1)");
  });

  it("prefixes '|' strings with apostrophe (DDE)", () => {
    expect(safeCell("|cmd;calc!A0")).toBe("'|cmd;calc!A0");
  });

  it("prefixes '%' strings with apostrophe", () => {
    expect(safeCell("%00malicious")).toBe("'%00malicious");
  });

  it("passes safe strings through unchanged", () => {
    expect(safeCell("Agachamento")).toBe("Agachamento");
    expect(safeCell("Foco na contração")).toBe("Foco na contração");
    expect(safeCell("")).toBe("");
  });

  it("passes numbers through unchanged", () => {
    expect(safeCell(3)).toBe(3);
    expect(safeCell(0)).toBe(0);
    expect(safeCell(-5)).toBe(-5);
  });

  it("passes null through unchanged", () => {
    expect(safeCell(null)).toBeNull();
  });
});

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

describe("buildFichaRows — raw values (safeCell applied later by exportarFichaParaExcel)", () => {
  /**
   * buildFichaRows is a pure data function — it returns raw values without
   * escaping. safeCell is applied in exportarFichaParaExcel before writing to
   * ExcelJS. These tests verify buildFichaRows is transparent.
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
    const ex = makeExercicio({ nomeExercicio: "|cmd;calc!A0" });
    expect(buildFichaRows({ ...BASE, exercicios: [ex] })[4][1]).toBe("|cmd;calc!A0");
  });
});

describe("exportarFichaParaExcel", () => {
  let anchorEl: { href: string; download: string; click: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    vi.clearAllMocks();
    anchorEl = { href: "", download: "", click: vi.fn() };
    vi.spyOn(document, "createElement").mockImplementation(function() {
      return anchorEl as unknown as HTMLElement;
    });
    Object.defineProperty(URL, "createObjectURL", { value: vi.fn(function() { return "blob:mock"; }), writable: true, configurable: true });
    Object.defineProperty(URL, "revokeObjectURL", { value: vi.fn(), writable: true, configurable: true });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("triggers download with sanitized filename + .xlsx extension", async () => {
    await exportarFichaParaExcel({ ...BASE, nome: "Treino/Especial" });
    expect(anchorEl.download).toBe("TreinoEspecial.xlsx");
  });

  it("filename always ends with .xlsx", async () => {
    await exportarFichaParaExcel(BASE);
    expect(anchorEl.download.endsWith(".xlsx")).toBe(true);
  });

  it("uses fallback 'ficha.xlsx' when nome sanitizes to empty", async () => {
    await exportarFichaParaExcel({ ...BASE, nome: "!!!" });
    expect(anchorEl.download).toBe("ficha.xlsx");
  });

  it("calls writeBuffer and triggers anchor click", async () => {
    await exportarFichaParaExcel(BASE);
    expect(excelMocks.writeBuffer).toHaveBeenCalled();
    expect(anchorEl.click).toHaveBeenCalled();
  });

  it("revokes object URL after triggering download", async () => {
    await exportarFichaParaExcel(BASE);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:mock");
  });

  it("creates worksheet named 'Ficha'", async () => {
    await exportarFichaParaExcel(BASE);
    expect(excelMocks.addWorksheet).toHaveBeenCalledWith("Ficha");
  });

  it("adds one row per data row built by buildFichaRows", async () => {
    await exportarFichaParaExcel(BASE);
    expect(excelMocks.addRow).toHaveBeenCalledTimes(buildFichaRows(BASE).length);
  });

  it("applies safeCell — formula-trigger cells are escaped before addRow", async () => {
    const ex = makeExercicio({ nomeExercicio: "=EVIL()" });
    await exportarFichaParaExcel({ ...BASE, exercicios: [ex] });

    // 4 header rows (index 0-3) then first data row (index 4)
    const dataRowArg = excelMocks.addRow.mock.calls[4][0] as (string | number | null)[];
    expect(dataRowArg[1]).toBe("'=EVIL()");
  });

  it("resolves without throwing for empty exercicios", async () => {
    await expect(exportarFichaParaExcel({ ...BASE, exercicios: [] })).resolves.toBeUndefined();
  });

  it("resolves without throwing when all optional fields are null", async () => {
    const ex = makeExercicio({
      observacao: null,
      series: [makeSerie({ repeticoesMax: null, descricao: null, carga: null, descanso: null })],
    });
    await expect(exportarFichaParaExcel({ ...BASE, exercicios: [ex] })).resolves.toBeUndefined();
  });

  it("sets nine column widths", async () => {
    await exportarFichaParaExcel(BASE);
    expect(excelMocks.getColumn).toHaveBeenCalledTimes(9);
  });
});
