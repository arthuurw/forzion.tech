"use client";
import { useState, useCallback } from "react";

export interface ConsentPreferences {
  v: 1;
  analytics: boolean;
}

const COOKIE_NAME = "consent";
const COOKIE_VERSION = 1;

function parseConsentCookie(): ConsentPreferences | null {
  if (typeof document === "undefined") return null;
  const match = document.cookie
    .split("; ")
    .find((row) => row.startsWith(`${COOKIE_NAME}=`));
  if (!match) return null;
  try {
    const value = decodeURIComponent(match.split("=").slice(1).join("="));
    const parsed = JSON.parse(value) as ConsentPreferences;
    if (parsed?.v === COOKIE_VERSION) return parsed;
  } catch {
    // ignore malformed cookie
  }
  return null;
}

function writeConsentCookie(prefs: ConsentPreferences) {
  if (typeof document === "undefined") return;
  const value = encodeURIComponent(JSON.stringify(prefs));
  const maxAge = 60 * 60 * 24 * 365;
  document.cookie = `${COOKIE_NAME}=${value}; max-age=${maxAge}; path=/; SameSite=Lax`;
}

/**
 * Returns the raw consent cookie value without React state (for server-side
 * or non-component use, e.g. Sentry init guard).
 */
export function readConsentCookie(): ConsentPreferences | null {
  return parseConsentCookie();
}

export interface UseConsentReturn {
  /** null = not yet chosen (show banner) */
  consent: ConsentPreferences | null;
  acceptAll: () => void;
  acceptEssential: () => void;
  savePreferences: (analytics: boolean) => void;
}

export function useConsent(): UseConsentReturn {
  const [consent, setConsent] = useState<ConsentPreferences | null>(() =>
    parseConsentCookie(),
  );

  const persist = useCallback((prefs: ConsentPreferences) => {
    writeConsentCookie(prefs);
    setConsent(prefs);
  }, []);

  const acceptAll = useCallback(() => {
    persist({ v: 1, analytics: true });
  }, [persist]);

  const acceptEssential = useCallback(() => {
    persist({ v: 1, analytics: false });
  }, [persist]);

  const savePreferences = useCallback(
    (analytics: boolean) => {
      persist({ v: 1, analytics });
    },
    [persist],
  );

  return { consent, acceptAll, acceptEssential, savePreferences };
}
