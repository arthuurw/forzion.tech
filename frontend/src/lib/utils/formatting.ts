import type { SerieConfigResponse } from "@/types";

// Construído uma vez no módulo: instanciar Intl.NumberFormat por chamada é caro.
const brlFormatter = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

/**
 * formatarBRL — formats a number as Brazilian Real currency (e.g. R$ 1.234,56).
 * Zero returns "Gratuito" only when `gratuito` is true (default false), so callers
 * that display "R$ 0,00" for paid contexts can opt out of the "Gratuito" label.
 */
export function formatarBRL(valor: number, gratuito = false): string {
  if (gratuito && valor === 0) return "Gratuito";
  return brlFormatter.format(valor);
}

/**
 * formatarTelefone — applies Brazilian phone mask to a digit-only or raw string.
 * Supports 10-digit (landline) and 11-digit (mobile) formats.
 */
export function formatarTelefone(phone: string): string {
  const d = phone.replace(/\D/g, "");
  if (d.length === 11) return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`;
  if (d.length === 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`;
  return phone;
}

export function formatarSeries(series: SerieConfigResponse[]): string {
  if (!series || series.length === 0) return "—";
  return series
    .map((s) => {
      const reps = s.repeticoesMax ? `${s.repeticoesMin}–${s.repeticoesMax}` : `${s.repeticoesMin}`;
      const label = s.descricao ? ` (${s.descricao})` : "";
      return `${s.quantidade}×${reps}${label}`;
    })
    .join(" / ");
}

export function formatarData(iso: string): string {
  const [, m, day] = iso.split("T")[0].split("-");
  return `${day}/${m}`;
}

export function getWeekLabel(dateStr: string): string {
  const d = new Date(dateStr);
  const day = d.getDay() || 7;
  d.setDate(d.getDate() - day + 1);
  return `${String(d.getDate()).padStart(2, "0")}/${String(d.getMonth() + 1).padStart(2, "0")}`;
}

export function periodoParaDatas(periodo: string): { de: string; ate: string } {
  const ate = new Date();
  const de = new Date();
  if (periodo === "7d")        de.setDate(de.getDate() - 7);
  else if (periodo === "30d")  de.setDate(de.getDate() - 30);
  else if (periodo === "60d")  de.setDate(de.getDate() - 60);
  else if (periodo === "90d")  de.setDate(de.getDate() - 90);
  else if (periodo === "6m")   de.setMonth(de.getMonth() - 6);
  else if (periodo === "1a")   de.setFullYear(de.getFullYear() - 1);
  else                         de.setFullYear(de.getFullYear() - 10);
  return {
    de: de.toISOString().split("T")[0],
    ate: ate.toISOString().split("T")[0],
  };
}
