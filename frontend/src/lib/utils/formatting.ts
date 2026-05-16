import type { SerieConfigResponse } from "@/types";

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
