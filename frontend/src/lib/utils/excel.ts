import * as XLSX from "xlsx";
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
 * Builds the flat row matrix that becomes the Excel sheet.
 * Exported as a pure function so it can be unit-tested without XLSX I/O.
 *
 * Security: SheetJS stores plain JS strings as type 's' (string) cells in xlsx,
 * so values like "=HYPERLINK(...)" are written as literal text — Excel will not
 * evaluate them as formulas. No additional escaping is performed because doing
 * so (e.g., prefixing with "'") would corrupt displayed values for legitimate data.
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

export function exportarFichaParaExcel(ficha: FichaExportParams): void {
  const wb = XLSX.utils.book_new();
  const ws = XLSX.utils.aoa_to_sheet(buildFichaRows(ficha));

  ws["!cols"] = [
    { wch: 4 },
    { wch: 32 },
    { wch: 12 },
    { wch: 10 },
    { wch: 10 },
    { wch: 20 },
    { wch: 12 },
    { wch: 13 },
    { wch: 40 },
  ];

  XLSX.utils.book_append_sheet(wb, ws, "Ficha");
  XLSX.writeFile(wb, `${sanitizeFilename(ficha.nome)}.xlsx`);
}
