import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import tsconfigPaths from "vite-tsconfig-paths";
import { resolve } from "path";

/**
 * Vitest configurado em 3 projects:
 * - "unit"       — env node, codigo puro (lib, hooks, middleware)
 * - "integration"— env jsdom, componentes/paginas React, com jest-dom + polyfills
 * - "api"        — env node, Next.js Route Handlers (src/app/api/**)
 *
 * Tests sao colocados perto do codigo testado.
 *
 * Coverage por path: thresholds diferenciados por camada (lib mais rigoroso,
 * app/* mais permissivo por boilerplate Next).
 */
export default defineConfig({
  plugins: [tsconfigPaths(), react()],
  resolve: {
    alias: {
      "next/image": resolve(__dirname, "src/test/mocks/nextImage.tsx"),
    },
  },
  test: {
    globals: true,
    // Render de server-components + cold imports ficam lentos sob instrumentação
    // de coverage (v8); o default de 5s estoura em CI/local. Herdado pelos projects
    // (extends: true). Não enfraquece asserção — só acomoda overhead de import.
    testTimeout: 20000,
    coverage: {
      provider: "v8",
      // Thresholds abaixo sao ENFORCED automaticamente quando `vitest run --coverage`
      // executa: vitest v4 falha com exit code != 0 se qualquer threshold por glob
      // nao for atingido. `npm run test:coverage` (= `vitest run --coverage`) e
      // chamado em CI no job `test-frontend`, entao o gate e bloqueante. Nao precisa
      // de flag `--check-coverage` (esse e linguajar de nyc, nao vitest).
      reporter: ["text", "json", "json-summary", "html", "lcov", "text-summary"],
      exclude: [
        "node_modules/",
        "src/test/**",
        "src/types/**",
        "**/*.d.ts",
        "**/*.config.*",
        "**/*.stories.tsx",
        "**/*.property.test.ts",
        "**/__tests__/**",
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
        // Fase 6 atingiu target final em src/app/api/** (cobertura completa
        // dos 8 route handlers). src/app/** ainda em baseline — pages Next
        // serao expandidas em fases futuras (E2E + storybook + a11y).
        "src/app/api/**": {
          lines: 90,
          branches: 85,
          functions: 90,
          statements: 90,
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
            "src/lib/**/*.property.test.ts",
            "src/hooks/**/*.test.ts",
            "src/hooks/**/*.property.test.ts",
            "src/middleware.test.ts",
            "src/middleware.signature.test.ts",
          ],
          exclude: [
            // Testes em src/lib/** que dependem de DOM rodam no project integration
            "src/lib/utils/excel.test.ts",
            "src/lib/utils/downloadBlob.test.ts",
            "src/lib/api/admin.msw.test.ts",
            "src/lib/auth/context.test.tsx",
            "src/hooks/useInactivity.test.ts",
            // Hooks RTL (renderHook) exigem jsdom → project integration
            "src/hooks/useConsent.test.ts",
            "src/hooks/usePaginatedList.test.ts",
            "src/hooks/useCRUDDialog.test.ts",
            "src/hooks/useCursorList.test.ts",
            "src/hooks/useExecucaoDraft.test.ts",
            "src/hooks/useExecucaoRetryQueue.test.ts",
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
            "src/components/**/__tests__/*.test.tsx",
            "src/app/**/__tests__/*.test.tsx",
            "src/app/**/*.client.test.tsx",
            // Tests em src/lib que mexem com DOM ou React (download, hooks RTL)
            "src/lib/utils/excel.test.ts",
            "src/lib/utils/downloadBlob.test.ts",
            "src/lib/auth/context.test.tsx",
            "src/hooks/useInactivity.test.ts",
            "src/hooks/useConsent.test.ts",
            "src/hooks/usePaginatedList.test.ts",
            "src/hooks/useCRUDDialog.test.ts",
            "src/hooks/useCursorList.test.ts",
            "src/hooks/useExecucaoDraft.test.ts",
            "src/hooks/useExecucaoRetryQueue.test.ts",
            // Pilot MSW com apiClient real
            "src/lib/api/admin.msw.test.ts",
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
          include: ["src/app/api/**/*.test.ts"],
        },
      },
    ],
  },
});
