export const DIAS_OPTIONS = [1, 2, 3, 4, 5, 6, 7].map((d) => ({
  value: String(d),
  label: `${d} dia${d > 1 ? "s" : ""} por semana`,
}));

export const TEMPO_OPTIONS = [
  { value: "30", label: "30 minutos" },
  { value: "45", label: "45 minutos" },
  { value: "60", label: "1 hora" },
  { value: "90", label: "1h30" },
  { value: "120", label: "2 horas ou mais" },
];

export const FINALIDADE_OPTIONS = [
  { value: "Hipertrofia", label: "Hipertrofia" },
  { value: "Emagrecimento", label: "Emagrecimento" },
  { value: "CondicionamentoFisico", label: "Condicionamento Físico" },
  { value: "Saude", label: "Saúde Geral" },
  { value: "PerformanceEsportiva", label: "Performance Esportiva" },
  { value: "Reabilitacao", label: "Reabilitação" },
  { value: "Outro", label: "Outro" },
];

export const NIVEL_OPTIONS = [
  { value: "Sedentario", label: "Sedentário (sem atividade regular)" },
  { value: "Iniciante", label: "Iniciante (menos de 1 ano de treino)" },
  { value: "Intermediario", label: "Intermediário (1–3 anos de treino)" },
  { value: "Avancado", label: "Avançado (mais de 3 anos de treino)" },
];
