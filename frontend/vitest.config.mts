import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import tsconfigPaths from "vite-tsconfig-paths";

export default defineConfig({
  plugins: [tsconfigPaths(), react()],
  test: {
    environment: "happy-dom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    coverage: {
      provider: "v8",
      reporter: ["text", "json", "html"],
      exclude: [
        "node_modules/",
        "src/test/**",
        "src/types/**",
        "src/app/api/**",
        "**/*.d.ts",
        "**/*.config.*",
        "src/main.tsx",
      ],
      thresholds: {
        branches: 40,
        functions: 43,
        lines: 57,
        statements: 54,
      },
    },
  },
});
