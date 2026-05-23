import next from "eslint-config-next";
import testingLibrary from "eslint-plugin-testing-library";
import jestDom from "eslint-plugin-jest-dom";
import security from "eslint-plugin-security";

export default [
  ...next,

  // Plugin "security" — heuristicas de codigo inseguro (eval, child_process, etc).
  // detect-object-injection eh ruidoso e gera falsos positivos em props/maps
  // legitimos; desligado globalmente.
  security.configs.recommended,
  {
    rules: {
      "security/detect-object-injection": "off",
    },
  },

  // Plugins de teste — apenas em arquivos de teste.
  // Configurados como warn nesta fase; promover a error em fase de "lint hardening"
  // depois de auto-fix em massa (~150 violacoes auto-corrigiveis).
  {
    files: [
      "**/*.test.ts",
      "**/*.test.tsx",
      "**/*.property.test.ts",
      "**/__tests__/**/*.{ts,tsx}",
    ],
    plugins: {
      "testing-library": testingLibrary,
      "jest-dom": jestDom,
    },
    rules: {
      ...testingLibrary.configs.react.rules,
      ...jestDom.configs.recommended.rules,
      // Rebaixadas para warn — codigo existente usa pattern legacy.
      "jest-dom/prefer-in-document": "warn",
      "jest-dom/prefer-to-have-text-content": "warn",
      "jest-dom/prefer-enabled-disabled": "warn",
      "testing-library/prefer-find-by": "warn",
      "testing-library/no-wait-for-side-effects": "warn",
      "testing-library/no-node-access": "warn",
      "testing-library/no-container": "warn",
      // Falsos positivos comuns em tests.
      "security/detect-non-literal-fs-filename": "off",
    },
  },

  // Regras novas Next 16 / React 19 — warn ate "lint hardening" fase futura
  // (requer refactor de hooks com setState in effect).
  {
    rules: {
      "react-hooks/purity": "warn",
      "react-hooks/set-state-in-effect": "warn",
      "react-hooks/exhaustive-deps": "warn",
      "import/no-anonymous-default-export": "warn",
    },
  },

  // Scripts utilitarios (Node).
  {
    files: ["scripts/**/*.mjs", "scripts/**/*.js"],
    rules: {
      "security/detect-non-literal-fs-filename": "off",
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
      "openapi.json",
      "src/test/msw/types.ts",
      "*.tsbuildinfo",
    ],
  },
];
