import type { ObjetivoTreino, TreinoExercicioResponse } from "@/types";
import { OBJETIVO_LABEL } from "@/lib/constants/labels";

export interface FichaExportParams {
  nome: string;
  objetivo: ObjetivoTreino;
  exercicios: TreinoExercicioResponse[];
}

/**
 * Strips characters that are invalid or dangerous in filenames.
 * Allows only ASCII word chars, whitespace, and hyphens.
 * Prevents path traversal and formula injection via the filename field.
 */
export function sanitizeFilename(nome: string): string {
  return nome.replace(/[^\w\s\-]/g, "").trim() || "ficha";
}

/**
 * ExcelJS evaluates strings starting with formula triggers (=, +, -, @, |, %)
 * as formulas. Prefix with "'" to force text interpretation — this is Excel's
 * inline string marker and is not displayed to the user.
 */
export function safeCell(v: string | number | null): string | number | null {
  if (typeof v === "string" && /^[=+\-@|%]/.test(v)) return "'" + v;
  return v;
}

/**
 * Builds the flat row matrix that becomes the Excel sheet.
 * Exported as a pure function so it can be unit-tested without I/O.
 * Returns raw values — caller applies safeCell before writing to ExcelJS.
 */
export function buildFichaRows(
  { nome, objetivo, exercicios }: FichaExportParams,
): (string | number | null)[][] {
  const sorted = [...exercicios].sort((a, b) => a.ordem - b.ordem);

  const rows: (string | number | null)[][] = [
    ["Ficha de Treino", nome],
    ["Objetivo", OBJETIVO_LABEL[objetivo] ?? objetivo],
    [],
    ["#", "Exercício", "Qtd Séries", "Reps Mín", "Reps Máx", "Descrição", "Carga (kg)", "Descanso (s)", "Observação"],
  ];

  sorted.forEach((ex, ei) => {
    const series = [...ex.series].sort((a, b) => a.ordem - b.ordem);

    if (series.length === 0) {
      rows.push([ei + 1, ex.nomeExercicio, null, null, null, null, null, null, ex.observacao ?? null]);
      return;
    }

    series.forEach((s, si) => {
      rows.push([
        si === 0 ? ei + 1 : null,
        si === 0 ? ex.nomeExercicio : null,
        s.quantidade,
        s.repeticoesMin,
        s.repeticoesMax ?? null,
        s.descricao ?? null,
        s.carga ?? null,
        s.descanso ?? null,
        si === 0 ? (ex.observacao ?? null) : null,
      ]);
    });
  });

  return rows;
}

export async function exportarFichaParaExcel(ficha: FichaExportParams): Promise<void> {
  const { default: ExcelJS } = await import("exceljs");
  const workbook = new ExcelJS.Workbook();
  const worksheet = workbook.addWorksheet("Ficha");

  const widths = [4, 32, 12, 10, 10, 20, 12, 13, 40];
  widths.forEach((w, i) => { worksheet.getColumn(i + 1).width = w; });

  buildFichaRows(ficha).forEach(row => worksheet.addRow(row.map(safeCell)));

  const buffer = await workbook.xlsx.writeBuffer();
  const blob = new Blob([buffer], {
    type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `${sanitizeFilename(ficha.nome)}.xlsx`;
  a.click();
  URL.revokeObjectURL(url);
}
