import type { DificuldadeTreino, ObjetivoTreino, PagamentoStatus } from "@/types";

type MuiChipColor = "default" | "success" | "warning" | "error" | "info";

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

// Admin dashboard pie/stat colors. Trainer palette mirrors ALUNO_STATUS_COLORS
// but keys differ ("Pendentes" vs "Aguardando"), so it is re-mapped here.
export const TREINADOR_STATUS_COLORS: Record<string, string> = {
  Ativos: ALUNO_STATUS_COLORS.Ativos,
  Pendentes: ALUNO_STATUS_COLORS.Aguardando,
  Inativos: ALUNO_STATUS_COLORS.Inativos,
};

export const ALUNO_DASHBOARD_STATUS_COLORS: Record<string, string> = {
  Ativos: "#2196f3",
  Pendentes: "#ff9800",
  Inativos: "#9e9e9e",
};

export const PAGAMENTO_STATUS_COLORS: Record<PagamentoStatus, MuiChipColor> = {
  Pago: "success",
  Pendente: "warning",
  Expirado: "default",
  Falhou: "error",
  Estornado: "info",
  EmDisputa: "error",
};

export const PAGAMENTO_STATUS_LABEL: Record<PagamentoStatus, string> = {
  Pago: "Pago",
  Pendente: "Pendente",
  Expirado: "Expirado",
  Falhou: "Falhou",
  Estornado: "Estornado",
  EmDisputa: "Em disputa",
};
