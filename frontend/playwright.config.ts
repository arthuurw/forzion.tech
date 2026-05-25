import { defineConfig, devices } from "@playwright/test";
import path from "node:path";

/**
 * Fase 9 do harness — Playwright base + sharding + network fixtures + auth states.
 *
 * - 3 navegadores desktop + 2 viewports mobile (5 projects).
 * - Project "setup" dependency: gera storage states em e2e/.auth/{admin,aluno,treinador}.json.
 * - Sharding via CLI (--shard 1/4); CI usa matrix em Fase 17.
 * - Trace/video/screenshot retidos so em retry/falha pra reduzir custo de armazenamento.
 *
 * Tests usam `import { test } from "../fixtures/test-base"` pra ganhar fixtures
 * tipadas de auth (loadAuth) e network (slow3G, offline, retry).
 *
 * webServer NAO eh iniciado pelo Playwright — assumimos:
 * - dev: `npm run dev` em terminal separado, ou E2E_BASE_URL apontando pra remoto;
 * - CI: build estatico + `npm run start` em job dedicado (Fase 17).
 */

const BASE_URL = process.env.E2E_BASE_URL ?? "http://localhost:3000";
const IS_CI = !!process.env.CI;
const AUTH_DIR = path.resolve(__dirname, "e2e/.auth");

export default defineConfig({
  testDir: "./e2e/specs",
  outputDir: "./test-results",
  fullyParallel: true,
  forbidOnly: IS_CI,
  retries: IS_CI ? 2 : 0,
  workers: IS_CI ? 4 : undefined,
  timeout: 30_000,
  expect: {
    timeout: 5_000,
    toHaveScreenshot: { maxDiffPixelRatio: 0.01, threshold: 0.2 },
  },
  reporter: IS_CI
    ? [
        ["html", { open: "never" }],
        ["junit", { outputFile: "playwright-report/junit.xml" }],
        ["github"],
        ["blob"],
      ]
    : [["html", { open: "never" }], ["list"]],
  use: {
    baseURL: BASE_URL,
    trace: "on-first-retry",
    video: "retain-on-failure",
    screenshot: "only-on-failure",
    locale: "pt-BR",
    timezoneId: "America/Sao_Paulo",
    actionTimeout: 10_000,
    navigationTimeout: 15_000,
  },
  projects: [
    {
      name: "setup",
      testDir: "./e2e",
      testMatch: /auth\.setup\.ts/,
    },

    // Desktop browsers — autenticados via storage state quando teste opt-in.
    {
      name: "chromium-desktop",
      use: { ...devices["Desktop Chrome"] },
      dependencies: ["setup"],
    },
    {
      name: "firefox-desktop",
      use: { ...devices["Desktop Firefox"] },
      dependencies: ["setup"],
    },
    {
      name: "webkit-desktop",
      use: { ...devices["Desktop Safari"] },
      dependencies: ["setup"],
    },

    // Mobile viewports — so em main/CI nightly (filtrado por grep ou matrix em
    // Fase 17). Mantemos a config aqui pra parametrizar PoM mobile cedo.
    {
      name: "mobile-chrome",
      use: { ...devices["Pixel 5"] },
      dependencies: ["setup"],
    },
    {
      name: "mobile-safari",
      use: { ...devices["iPhone 13"] },
      dependencies: ["setup"],
    },
  ],
  metadata: {
    authDir: AUTH_DIR,
    baseURL: BASE_URL,
  },
});
