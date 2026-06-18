import type { TreinoExercicioResponse } from "@/types";

export type SetState = { reps: string; carga: string; groupLabel?: string };

export function initExecData(exercicios: TreinoExercicioResponse[]): Record<string, SetState[]> {
  const map: Record<string, SetState[]> = {};
  for (const ex of exercicios) {
    const sets: SetState[] = [];
    for (const s of [...(ex.series ?? [])].sort((a, b) => a.ordem - b.ordem)) {
      for (let i = 0; i < s.quantidade; i++) {
        sets.push({
          reps: String(s.repeticoesMin),
          carga: s.carga != null ? String(s.carga) : "",
          groupLabel: s.descricao || undefined,
        });
      }
    }
    map[ex.treinoExercicioId] = sets;
  }
  return map;
}
