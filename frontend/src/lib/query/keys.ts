export const queryKeys = {
  catalog: {
    gruposMusculares: ["catalog", "grupos-musculares"] as const,
    planos: ["catalog", "planos"] as const,
  },
  admin: {
    dashboard: ["admin", "dashboard"] as const,
  },
  treinador: {
    dashboard: ["treinador", "dashboard"] as const,
  },
  aluno: {
    dashboard: ["aluno", "dashboard"] as const,
  },
} as const;
