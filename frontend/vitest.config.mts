import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import tsconfigPaths from "vite-tsconfig-paths";

/**
 * Vitest configurado em 3 projects:
 * - "unit"       — env node, codigo puro (lib, hooks, utils, validations, middleware)
 * - "integration"— env jsdom, componentes/paginas React, com jest-dom + polyfills
 * - "api"        — env node, Next.js Route Handlers (src/app/api/**)
 *
 * Tests em src/test/* sao transitorios: serao colocados (Fase 5) e a entrada
 * por nome desaparece. Por enquanto cada arquivo eh atribuido manualmente ao
 * project correto.
 *
 * Coverage por path: thresholds diferenciados por camada (lib mais rigoroso,
 * app/* mais permissivo por boilerplate Next).
 */
export default defineConfig({
  plugins: [tsconfigPaths(), react()],
  test: {
    globals: true,
    coverage: {
      provider: "v8",
      reporter: ["text", "json", "html", "lcov", "text-summary"],
      exclude: [
        "node_modules/",
        "src/test/**",
        "src/types/**",
        "**/*.d.ts",
        "**/*.config.*",
        "**/*.stories.tsx",
        "**/*.property.test.ts",
        "e2e/**",
        "src/main.tsx",
      ],
      thresholds: {
        "src/lib/**": {
          lines: 95,
          branches: 90,
          functions: 95,
          statements: 95,
        },
        "src/hooks/**": {
          lines: 90,
          branches: 85,
          functions: 90,
          statements: 90,
        },
        "src/components/**": {
          lines: 85,
          branches: 75,
          functions: 85,
          statements: 85,
        },
        // Baseline Fase 2 — refletindo cobertura atual sem regressao.
        // Targets finais (Fase 5/6 quando testes migrarem e API routes expandirem):
        // src/app/api/**: 90L / 85B / 90F / 90S
        // src/app/**:     70L / 60B / 70F / 70S
        "src/app/api/**": {
          lines: 85,
          branches: 80,
          functions: 90,
          statements: 85,
        },
        "src/app/**": {
          lines: 70,
          branches: 60,
          functions: 55,
          statements: 65,
        },
      },
    },
    projects: [
      {
        extends: true,
        plugins: [tsconfigPaths(), react()],
        test: {
          name: "unit",
          environment: "node",
          pool: "threads",
          setupFiles: ["./src/test/setup/unit.ts"],
          include: [
            "src/lib/**/*.test.ts",
            "src/hooks/**/*.test.ts",
            // Transicao: testes ainda em src/test/ que rodam em env node
            "src/test/admin-api.test.ts",
            "src/test/auth.test.ts",
            "src/test/formatting.test.ts",
            "src/test/middleware.test.ts",
            "src/test/rateLimit.test.ts",
            "src/test/validations.test.ts",
          ],
          exclude: [
            // Testes em src/lib/** que dependem de DOM rodam no project integration
            "src/lib/utils/excel.test.ts",
          ],
        },
      },
      {
        extends: true,
        plugins: [tsconfigPaths(), react()],
        test: {
          name: "integration",
          environment: "jsdom",
          pool: "forks",
          setupFiles: ["./src/test/setup/integration.ts"],
          include: [
            "src/components/**/*.test.tsx",
            "src/app/**/*.client.test.tsx",
            // Tests em src/lib que mexem com DOM (download via anchor, etc)
            "src/lib/utils/excel.test.ts",
            // Transicao: testes ainda em src/test/ que precisam de DOM
            "src/test/admin-pages.test.tsx",
            "src/test/auth-context.test.tsx",
            "src/test/components.test.tsx",
            "src/test/pagamento.test.tsx",
            "src/test/pagamento-cartao.test.tsx",
            "src/test/responsive-table.test.tsx",
            "src/test/useInactivity.test.ts",
            "src/test/msw-pilot.test.ts",
          ],
        },
      },
      {
        extends: true,
        plugins: [tsconfigPaths(), react()],
        test: {
          name: "api",
          environment: "node",
          pool: "threads",
          setupFiles: ["./src/test/setup/api.ts"],
          include: [
            "src/app/api/**/*.test.ts",
            // Transicao
            "src/test/api-auth-me.test.ts",
            "src/test/api-auth-route.test.ts",
          ],
        },
      },
    ],
  },
});
