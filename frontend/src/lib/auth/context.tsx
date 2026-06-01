"use client";

import {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  ReactNode,
} from "react";
import { useRouter } from "next/navigation";
import type { LoginResponse, SessionUser, TipoConta } from "@/types";

interface AuthContextValue {
  user: SessionUser | null;
  isLoading: boolean;
  login: (data: LoginResponse) => void;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<SessionUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const router = useRouter();

  useEffect(() => {
    const controller = new AbortController();
    fetch("/api/auth/me", { signal: controller.signal })
      .then((r) => (r.ok ? r.json() : null))
      .then((data: SessionUser | null) => {
        if (controller.signal.aborted) return;
        setUser(data);
        setIsLoading(false);
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted || (err instanceof Error && err.name === "AbortError")) return;
        setUser(null);
        setIsLoading(false);
      });
    return () => controller.abort();
  }, []);

  const login = useCallback((data: LoginResponse) => {
    // O token não é armazenado no estado client-side — permanece apenas no httpOnly cookie.
    setUser({ contaId: data.contaId, perfilId: data.perfilId, tipoConta: data.tipoConta });
  }, []);

  const logout = useCallback(async () => {
    await fetch("/api/auth/logout", { method: "POST" });
    setUser(null);
    router.push("/login");
  }, [router]);

  return (
    <AuthContext.Provider value={{ user, isLoading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}

export function homeRouteFor(tipoConta: TipoConta): string {
  switch (tipoConta) {
    case "SystemAdmin":
      return "/admin";
    case "Treinador":
      return "/treinador";
    case "Aluno":
      return "/aluno";
  }
}
