import { defineConfig } from "vitest/config";
import tsconfigPaths from "vite-tsconfig-paths";

/**
 * Config isolada para os contratos Pact (Fase 15).
 *
 * Os testes consumer sobem um mock server nativo do Pact por interacao e
 * gravam pacts em disco — efeitos colaterais que NAO devem rodar no lane
 * padrao (`npm test` / pre-commit). Por isso ficam fora dos projects do
 * `vitest.config.mts` e rodam so via `npm run test:contract`.
 *
 * - env node: `apiClient` usa o adapter http do axios contra o mock server.
 * - singleFork + sem paralelismo: evita corrida por portas dos mock servers.
 */
export default defineConfig({
  plugins: [tsconfigPaths()],
  test: {
    globals: true,
    environment: "node",
    include: ["src/test/pact/**/*.test.ts"],
    pool: "forks",
    fileParallelism: false,
    testTimeout: 30000,
    hookTimeout: 30000,
  },
});
