"use client";
import { useEffect, useRef, useCallback } from "react";

const TIMEOUT_MS  = 20 * 60 * 1000; // 20 min → logout
const WARN_STEP   =  5 * 60 * 1000; // avisar a cada 5 min de inatividade
const CHECK_MS    = 20 * 1000;       // checar a cada 20 segundos

interface Options {
  onWarn: (minutesInactive: number) => void;
  onTimeout: () => void;
  enabled: boolean;
}

export function useInactivity({ onWarn, onTimeout, enabled }: Options) {
  const lastActivity = useRef(Date.now());
  const warnedSteps  = useRef(new Set<number>());

  const resetActivity = useCallback(() => {
    lastActivity.current = Date.now();
    warnedSteps.current.clear();
  }, []);

  useEffect(() => {
    if (!enabled) return;

    const events = ["mousemove", "mousedown", "keydown", "touchstart", "scroll", "click"];
    events.forEach((e) => window.addEventListener(e, resetActivity, { passive: true }));

    const interval = setInterval(() => {
      const elapsed = Date.now() - lastActivity.current;

      if (elapsed >= TIMEOUT_MS) {
        onTimeout();
        return;
      }

      // Quantos múltiplos de 5 min já passaram (mínimo 1 para começar a avisar)
      const step = Math.floor(elapsed / WARN_STEP);
      if (step >= 1 && !warnedSteps.current.has(step)) {
        warnedSteps.current.add(step);
        onWarn(step * 5);
      }
    }, CHECK_MS);

    return () => {
      events.forEach((e) => window.removeEventListener(e, resetActivity));
      clearInterval(interval);
    };
  }, [enabled, onWarn, onTimeout, resetActivity]);
}
