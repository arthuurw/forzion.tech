import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, it, expect } from "vitest";
import { JWT_SECRET_BUILD_PLACEHOLDER } from "./buildPlaceholder";

/**
 * Trava drift entre o literal `ARG JWT_SECRET=...` no Dockerfile e a constante
 * exportada usada pelo runtime guard em `instrumentation.ts`. Se um divergir,
 * o guard de produção pode passar com o placeholder ativo — o exato modo de
 * falha que originou a fix `e454127`.
 */
describe("JWT_SECRET_BUILD_PLACEHOLDER vs Dockerfile ARG", () => {
  it("literal no Dockerfile bate com a constante exportada", () => {
    const dockerfilePath = resolve(__dirname, "../../../Dockerfile");
    const content = readFileSync(dockerfilePath, "utf8");
    const match = content.match(/^ARG JWT_SECRET=(.+)$/m);
    expect(match, "ARG JWT_SECRET não encontrado no Dockerfile").not.toBeNull();
    expect(match![1].trim()).toBe(JWT_SECRET_BUILD_PLACEHOLDER);
  });
});
