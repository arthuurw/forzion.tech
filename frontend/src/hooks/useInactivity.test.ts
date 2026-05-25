import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useInactivity } from "@/hooks/useInactivity";

const WARN_STEP = 5 * 60 * 1000;
const TIMEOUT_MS = 20 * 60 * 1000;
const CHECK_MS = 20 * 1000;

describe("useInactivity", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it("disabled=false → callbacks nunca chamados", () => {
    const onWarn = vi.fn();
    const onTimeout = vi.fn();
    renderHook(() => useInactivity({ onWarn, onTimeout, enabled: false }));

    act(() => { vi.advanceTimersByTime(TIMEOUT_MS + CHECK_MS); });

    expect(onWarn).not.toHaveBeenCalled();
    expect(onTimeout).not.toHaveBeenCalled();
  });

  it("inatividade < 5min → sem warn", () => {
    const onWarn = vi.fn();
    const onTimeout = vi.fn();
    renderHook(() => useInactivity({ onWarn, onTimeout, enabled: true }));

    act(() => { vi.advanceTimersByTime(4 * 60 * 1000 + CHECK_MS); });

    expect(onWarn).not.toHaveBeenCalled();
    expect(onTimeout).not.toHaveBeenCalled();
  });

  it("inatividade >= 5min → onWarn(5) chamado", () => {
    const onWarn = vi.fn();
    const onTimeout = vi.fn();
    renderHook(() => useInactivity({ onWarn, onTimeout, enabled: true }));

    act(() => { vi.advanceTimersByTime(WARN_STEP + CHECK_MS); });

    expect(onWarn).toHaveBeenCalledWith(5);
    expect(onTimeout).not.toHaveBeenCalled();
  });

  it("inatividade >= 10min → onWarn(10) chamado (não repete o de 5min)", () => {
    const onWarn = vi.fn();
    const onTimeout = vi.fn();
    renderHook(() => useInactivity({ onWarn, onTimeout, enabled: true }));

    act(() => { vi.advanceTimersByTime(2 * WARN_STEP + CHECK_MS); });

    expect(onWarn).toHaveBeenCalledTimes(2);
    expect(onWarn).toHaveBeenCalledWith(5);
    expect(onWarn).toHaveBeenCalledWith(10);
  });

  it("inatividade >= 20min → onTimeout chamado", () => {
    const onWarn = vi.fn();
    const onTimeout = vi.fn();
    renderHook(() => useInactivity({ onWarn, onTimeout, enabled: true }));

    act(() => { vi.advanceTimersByTime(TIMEOUT_MS + CHECK_MS); });

    expect(onTimeout).toHaveBeenCalled();
  });

  it("evento de atividade reseta timer e limpa warns", () => {
    const onWarn = vi.fn();
    const onTimeout = vi.fn();
    renderHook(() => useInactivity({ onWarn, onTimeout, enabled: true }));

    // Avança 5min → warn esperado
    act(() => { vi.advanceTimersByTime(WARN_STEP + CHECK_MS); });
    expect(onWarn).toHaveBeenCalledWith(5);

    // Simula atividade — dispara mousemove
    act(() => { window.dispatchEvent(new Event("mousemove")); });

    // Avança mais 4min → sem novo warn (timer resetou)
    act(() => { vi.advanceTimersByTime(4 * 60 * 1000 + CHECK_MS); });
    expect(onWarn).toHaveBeenCalledTimes(1);
  });
});
