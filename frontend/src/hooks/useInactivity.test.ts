import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useInactivity } from "@/hooks/useInactivity";

const TIMEOUT_MS = 30 * 60 * 1000;
const WARN_LEAD_MS = 5 * 60 * 1000;
const WARN_AT = TIMEOUT_MS - WARN_LEAD_MS; // 25 min
const CHECK_MS = 20 * 1000;

describe("useInactivity", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it("enabled=false → callbacks nunca chamados", () => {
    const onWarn = vi.fn();
    const onTimeout = vi.fn();
    renderHook(() => useInactivity({ onWarn, onTimeout, enabled: false }));

    act(() => { vi.advanceTimersByTime(TIMEOUT_MS + CHECK_MS); });

    expect(onWarn).not.toHaveBeenCalled();
    expect(onTimeout).not.toHaveBeenCalled();
  });

  it("inatividade antes da janela de aviso → sem warn", () => {
    const onWarn = vi.fn();
    const onTimeout = vi.fn();
    renderHook(() => useInactivity({ onWarn, onTimeout, enabled: true }));

    act(() => { vi.advanceTimersByTime(WARN_AT - 60 * 1000); });

    expect(onWarn).not.toHaveBeenCalled();
    expect(onTimeout).not.toHaveBeenCalled();
  });

  it("inatividade >= 25min → aviso único de 5 min restantes", () => {
    const onWarn = vi.fn();
    const onTimeout = vi.fn();
    renderHook(() => useInactivity({ onWarn, onTimeout, enabled: true }));

    act(() => { vi.advanceTimersByTime(WARN_AT + CHECK_MS); });

    expect(onWarn).toHaveBeenCalledWith(5);
    expect(onTimeout).not.toHaveBeenCalled();
  });

  it("aviso não repete enquanto segue inativo", () => {
    const onWarn = vi.fn();
    const onTimeout = vi.fn();
    renderHook(() => useInactivity({ onWarn, onTimeout, enabled: true }));

    act(() => { vi.advanceTimersByTime(WARN_AT + CHECK_MS); });
    act(() => { vi.advanceTimersByTime(2 * 60 * 1000); });

    expect(onWarn).toHaveBeenCalledTimes(1);
  });

  it("inatividade >= 30min → onTimeout chamado", () => {
    const onWarn = vi.fn();
    const onTimeout = vi.fn();
    renderHook(() => useInactivity({ onWarn, onTimeout, enabled: true }));

    act(() => { vi.advanceTimersByTime(TIMEOUT_MS + CHECK_MS); });

    expect(onTimeout).toHaveBeenCalled();
  });

  it("evento de atividade reseta timer e limpa o aviso", () => {
    const onWarn = vi.fn();
    const onTimeout = vi.fn();
    renderHook(() => useInactivity({ onWarn, onTimeout, enabled: true }));

    act(() => { vi.advanceTimersByTime(WARN_AT + CHECK_MS); });
    expect(onWarn).toHaveBeenCalledWith(5);

    act(() => { window.dispatchEvent(new Event("mousemove")); });

    act(() => { vi.advanceTimersByTime(WARN_AT - 60 * 1000); });
    expect(onWarn).toHaveBeenCalledTimes(1);
  });
});
