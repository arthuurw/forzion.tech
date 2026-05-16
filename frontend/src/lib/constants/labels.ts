import type { DificuldadeTreino, ObjetivoTreino } from "@/types";

export const OBJETIVO_LABEL: Record<string, string> = {
  Hipertrofia: "Hipertrofia",
  Emagrecimento: "Emagrecimento",
  Resistencia: "Resistência",
  Forca: "Força",
  Flexibilidade: "Flexibilidade",
  Condicionamento: "Condicionamento",
};

export const OBJETIVOS: ObjetivoTreino[] = [
  "Hipertrofia", "Emagrecimento", "Resistencia", "Forca", "Flexibilidade", "Condicionamento",
];

export const OBJETIVOS_FILTRO: { value: string; label: string }[] = [
  { value: "Hipertrofia", label: "Hipertrofia" },
  { value: "Emagrecimento", label: "Emagrecimento" },
  { value: "Resistencia", label: "Resistência" },
  { value: "Forca", label: "Força" },
  { value: "Flexibilidade", label: "Flexibilidade" },
  { value: "Condicionamento", label: "Condicionamento" },
];

export const DIFICULDADE_LABEL: Record<DificuldadeTreino, string> = {
  Iniciante: "Iniciante",
  Intermediario: "Intermediário",
  Avancado: "Avançado",
};

export const DIFICULDADES: { value: DificuldadeTreino; label: string; color: string }[] = [
  { value: "Iniciante",     label: "Iniciante",     color: "#4caf50" },
  { value: "Intermediario", label: "Intermediário", color: "#ff9800" },
  { value: "Avancado",      label: "Avançado",      color: "#f44336" },
];

export const GRUPO_MUSCULAR_LABEL: Record<string, string> = {
  Biceps: "Bíceps",
  Triceps: "Tríceps",
  Gluteos: "Glúteos",
};

export const FINALIDADE_LABEL: Record<string, string> = {
  Hipertrofia: "Hipertrofia",
  Emagrecimento: "Emagrecimento",
  CondicionamentoFisico: "Condicionamento Físico",
  Saude: "Saúde Geral",
  PerformanceEsportiva: "Performance Esportiva",
  Reabilitacao: "Reabilitação",
  Outro: "Outro",
};

export const NIVEL_LABEL: Record<string, string> = {
  Sedentario: "Sedentário",
  Iniciante: "Iniciante",
  Intermediario: "Intermediário",
  Avancado: "Avançado",
};

export const TEMPO_LABEL: Record<string, string> = {
  TrintaMinutos: "30 min",
  QuarentaCincoMinutos: "45 min",
  UmaHora: "1 hora",
  UmaHoraETrinta: "1h30",
  DuasHoras: "2 horas ou mais",
};

export const ALUNO_STATUS_COLORS: Record<string, string> = {
  Ativos: "#4caf50",
  Aguardando: "#F5C400",
  Inativos: "#757575",
};
