import next from "eslint-config-next";

export default [
  ...next,
  {
    // Regras novas Next 16 / React 19 — relaxadas para warn nesta fase.
    // Endurecidas na Fase 7 (lint completo) do harness.
    rules: {
      "react-hooks/purity": "warn",
      "react-hooks/set-state-in-effect": "warn",
      "react-hooks/exhaustive-deps": "warn",
      "import/no-anonymous-default-export": "warn",
    },
  },
  {
    ignores: [
      "node_modules/**",
      ".next/**",
      "coverage/**",
      "out/**",
      "build/**",
      "storybook-static/**",
      "playwright-report/**",
      "test-results/**",
      "*.tsbuildinfo",
    ],
  },
];
