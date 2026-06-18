// @vitest-environment jsdom
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { safeGet, safeRemove, safeSet } from "./safeStorage";

describe("safeStorage", () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it("set então get retorna o valor gravado", () => {
    safeSet("k", "v");
    expect(safeGet("k")).toBe("v");
  });

  it("remove apaga a chave", () => {
    safeSet("k", "v");
    safeRemove("k");
    expect(safeGet("k")).toBeNull();
  });

  it("setItem lançando (quota) → no-op sem propagar", () => {
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new DOMException("quota", "QuotaExceededError");
    });
    expect(() => safeSet("k", "v")).not.toThrow();
  });

  it("localStorage indisponível (SSR) → get null, set/remove no-op", () => {
    vi.stubGlobal("localStorage", undefined);
    expect(safeGet("k")).toBeNull();
    expect(() => safeSet("k", "v")).not.toThrow();
    expect(() => safeRemove("k")).not.toThrow();
  });
});
