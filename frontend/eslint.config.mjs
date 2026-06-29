import next from "eslint-config-next";
import testingLibrary from "eslint-plugin-testing-library";
import jestDom from "eslint-plugin-jest-dom";
import security from "eslint-plugin-security";
import playwright from "eslint-plugin-playwright";
import reactHooks from "eslint-plugin-react-hooks";
import importPlugin from "eslint-plugin-import";

const config = [
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
      "security/detect-possible-timing-attacks": "off",
    },
  },

  {
    plugins: { "react-hooks": reactHooks, import: importPlugin },
    rules: {
      "react-hooks/purity": "off",
      "react-hooks/set-state-in-effect": "off",
      "react-hooks/exhaustive-deps": "warn",
      "import/no-anonymous-default-export": "warn",
    },
  },

  {
    files: ["scripts/**/*.mjs", "scripts/**/*.js"],
    rules: {
      "security/detect-non-literal-fs-filename": "off",
    },
  },

  // E2E (Playwright) — plugin com regras dedicadas. detect-non-literal-fs-filename
  // off porque storage states usam path.join() dinamico. rules-of-hooks off
  // porque o callback `use(fixture)` do Playwright bate falso positivo.
  {
    files: ["e2e/**/*.ts", "playwright.config.ts"],
    plugins: { playwright, "react-hooks": reactHooks },
    rules: {
      ...playwright.configs["flat/recommended"].rules,
      "security/detect-non-literal-fs-filename": "off",
      "react-hooks/rules-of-hooks": "off",
      "no-console": "off",
      "playwright/no-skipped-test": ["warn", { allowConditional: true }],
      "playwright/no-conditional-in-test": "off",
      "playwright/no-conditional-expect": "off",
      "playwright/expect-expect": ["warn", { assertFunctionNames: ["scanAxe", "runAxe"] }],
    },
  },

  {
    files: ["src/app/**/*.{ts,tsx}"],
    ignores: ["src/app/**/*.test.{ts,tsx}", "src/app/**/__tests__/**"],
    rules: {
      "no-restricted-imports": [
        "error",
        {
          paths: [
            {
              name: "@mui/material",
              importNames: ["Alert"],
              message:
                "Use <AlertBanner> (@/components/ui/AlertBanner) — canal canônico de feedback de erro/sucesso. Alert cru só com eslint-disable + motivo (ex.: erro terminal de pagamento, disclaimer estático role=note).",
            },
          ],
        },
      ],
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
      "public/mockServiceWorker.js",
      "*.tsbuildinfo",
    ],
  },
];

export default config;
