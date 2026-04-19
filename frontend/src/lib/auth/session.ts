import type { SessionUser } from "@/types";

const SESSION_KEY = "forzion_user";

export function saveSession(user: SessionUser): void {
  sessionStorage.setItem(SESSION_KEY, JSON.stringify(user));
}

export function loadSession(): SessionUser | null {
  if (typeof window === "undefined") return null;
  const raw = sessionStorage.getItem(SESSION_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as SessionUser;
  } catch {
    sessionStorage.removeItem(SESSION_KEY);
    return null;
  }
}

export function clearSession(): void {
  sessionStorage.removeItem(SESSION_KEY);
}
