import { describe, it, expect } from "vitest";
import { buildCsp } from "./csp";

describe("buildCsp", () => {
  it("inclui object-src 'none'", () => {
    expect(buildCsp(false)).toContain("object-src 'none'");
  });

  it("prod omite 'unsafe-eval'; dev inclui", () => {
    expect(buildCsp(false)).not.toContain("'unsafe-eval'");
    expect(buildCsp(true)).toContain("'unsafe-eval'");
  });
});
