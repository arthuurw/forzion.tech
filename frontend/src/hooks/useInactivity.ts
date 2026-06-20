"use client";
import { useEffect, useRef, useCallback } from "react";

const TIMEOUT_MS   = 30 * 60 * 1000; // 30 min → logout
const WARN_LEAD_MS =  5 * 60 * 1000; // aviso único 5 min antes do fim
const CHECK_MS     = 20 * 1000;       // checar a cada 20 segundos

interface Options {
  onWarn: (minutesRemaining: number) => void;
  onTimeout: () => void;
  enabled: boolean;
}

export function useInactivity({ onWarn, onTimeout, enabled }: Options) {
  const lastActivity = useRef(Date.now());
  const warned = useRef(false);

  const resetActivity = useCallback(() => {
    lastActivity.current = Date.now();
    warned.current = false;
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

      if (elapsed >= TIMEOUT_MS - WARN_LEAD_MS && !warned.current) {
        warned.current = true;
        onWarn(Math.ceil((TIMEOUT_MS - elapsed) / 60000));
      }
    }, CHECK_MS);

    return () => {
      events.forEach((e) => window.removeEventListener(e, resetActivity));
      clearInterval(interval);
    };
  }, [enabled, onWarn, onTimeout, resetActivity]);
}
