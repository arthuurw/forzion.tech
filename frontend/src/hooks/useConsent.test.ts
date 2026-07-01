import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useConsent, readConsentCookie } from "@/hooks/useConsent";

function clearCookies() {
  for (const c of document.cookie.split("; ")) {
    const name = c.split("=")[0];
    if (name) document.cookie = `${name}=; max-age=0; path=/`;
  }
}

describe("useConsent / readConsentCookie", () => {
  beforeEach(() => clearCookies());
  afterEach(() => clearCookies());

  it("sem cookie → consent null (mostra banner)", () => {
    const { result } = renderHook(() => useConsent());
    expect(result.current.consent).toBeNull();
    expect(readConsentCookie()).toBeNull();
  });

  it("acceptAll grava cookie com analytics=true", () => {
    const { result } = renderHook(() => useConsent());
    act(() => result.current.acceptAll());
    expect(result.current.consent).toEqual({ v: 1, analytics: true });
    expect(readConsentCookie()).toEqual({ v: 1, analytics: true });
  });

  it("acceptEssential grava analytics=false", () => {
    const { result } = renderHook(() => useConsent());
    act(() => result.current.acceptEssential());
    expect(result.current.consent).toEqual({ v: 1, analytics: false });
  });

  it("savePreferences grava o valor passado", () => {
    const { result } = renderHook(() => useConsent());
    act(() => result.current.savePreferences(true));
    expect(result.current.consent).toEqual({ v: 1, analytics: true });
  });

  it("cookie pré-existente válido é lido na montagem", () => {
    document.cookie = `consent=${encodeURIComponent(JSON.stringify({ v: 1, analytics: true }))}; path=/`;
    const { result } = renderHook(() => useConsent());
    expect(result.current.consent).toEqual({ v: 1, analytics: true });
  });

  it("cookie com versão divergente → null", () => {
    document.cookie = `consent=${encodeURIComponent(JSON.stringify({ v: 99, analytics: true }))}; path=/`;
    expect(readConsentCookie()).toBeNull();
  });

  it("cookie malformado (JSON inválido) → null (catch)", () => {
    document.cookie = `consent=${encodeURIComponent("{not-json")}; path=/`;
    expect(readConsentCookie()).toBeNull();
  });

  it("sob https anexa ; Secure ao consent cookie", () => {
    const writes: string[] = [];
    const spy = vi
      .spyOn(document, "cookie", "set")
      .mockImplementation((v) => {
        writes.push(v);
      });
    vi.stubGlobal("location", { protocol: "https:" });

    const { result } = renderHook(() => useConsent());
    act(() => result.current.acceptAll());

    expect(writes.at(-1)).toContain("; Secure");
    spy.mockRestore();
    vi.unstubAllGlobals();
  });

  it("sob http omite Secure", () => {
    const writes: string[] = [];
    const spy = vi
      .spyOn(document, "cookie", "set")
      .mockImplementation((v) => {
        writes.push(v);
      });
    vi.stubGlobal("location", { protocol: "http:" });

    const { result } = renderHook(() => useConsent());
    act(() => result.current.acceptAll());

    expect(writes.at(-1)).not.toContain("Secure");
    spy.mockRestore();
    vi.unstubAllGlobals();
  });
});
