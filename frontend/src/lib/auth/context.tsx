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
import { clearSession, loadSession, saveSession } from "./session";

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
    setUser(loadSession());
    setIsLoading(false);
  }, []);

  const login = useCallback(
    (data: LoginResponse) => {
      const session: SessionUser = {
        contaId: data.contaId,
        perfilId: data.perfilId,
        tipoConta: data.tipoConta,
        token: data.token,
      };
      saveSession(session);
      setUser(session);
      router.push(homeRouteFor(data.tipoConta));
    },
    [router]
  );

  const logout = useCallback(async () => {
    await fetch("/api/auth/logout", { method: "POST" });
    clearSession();
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
