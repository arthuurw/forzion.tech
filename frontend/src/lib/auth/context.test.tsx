import { afterEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { AuthProvider, useAuth } from "@/lib/auth/context";
import type { LoginResponse } from "@/types";

const mockPush = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush }),
}));

// ─── Helpers ─────────────────────────────────────────────────────────────────

const SESSION_USER = { contaId: "c1", perfilId: "p1", tipoConta: "Aluno" as const };

const LOGIN_PAYLOAD: LoginResponse = {
  token: "fake-token",
  contaId: "c1",
  perfilId: "p1",
  tipoConta: "Treinador",
};

function Consumer() {
  const { user, isLoading, login, logout } = useAuth();
  return (
    <>
      <div data-testid="loading">{String(isLoading)}</div>
      <div data-testid="user">{user?.tipoConta ?? "null"}</div>
      <button onClick={() => login(LOGIN_PAYLOAD)}>login</button>
      <button onClick={() => void logout()}>logout</button>
    </>
  );
}

function stubFetch(responses: { ok: boolean; body?: unknown }[]) {
  let call = 0;
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation(() => {
      const r = responses[call++] ?? { ok: false };
      return Promise.resolve({ ok: r.ok, json: async () => r.body ?? null });
    }),
  );
}

afterEach(() => {
  vi.clearAllMocks();
  vi.unstubAllGlobals();
});

// ─── Montagem / fetch /api/auth/me ───────────────────────────────────────────

describe("AuthProvider — GET /api/auth/me", () => {
  it("sucesso → popula user, isLoading false", async () => {
    stubFetch([{ ok: true, body: SESSION_USER }]);
    render(<AuthProvider><Consumer /></AuthProvider>);

    expect(screen.getByTestId("loading").textContent).toBe("true");

    await waitFor(() =>
      expect(screen.getByTestId("loading").textContent).toBe("false"),
    );
    expect(screen.getByTestId("user").textContent).toBe("Aluno");
  });

  it("resposta não-ok → user null, isLoading false", async () => {
    stubFetch([{ ok: false }]);
    render(<AuthProvider><Consumer /></AuthProvider>);

    await waitFor(() =>
      expect(screen.getByTestId("loading").textContent).toBe("false"),
    );
    expect(screen.getByTestId("user").textContent).toBe("null");
  });

  it("fetch rejeita → user null, isLoading false", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("Network error")));
    render(<AuthProvider><Consumer /></AuthProvider>);

    await waitFor(() =>
      expect(screen.getByTestId("loading").textContent).toBe("false"),
    );
    expect(screen.getByTestId("user").textContent).toBe("null");
  });
});

// ─── login() ─────────────────────────────────────────────────────────────────

describe("login()", () => {
  it("seta user com tipoConta do LoginResponse", async () => {
    stubFetch([{ ok: false }]);
    render(<AuthProvider><Consumer /></AuthProvider>);

    await waitFor(() =>
      expect(screen.getByTestId("loading").textContent).toBe("false"),
    );

    fireEvent.click(screen.getByRole("button", { name: "login" }));

    expect(screen.getByTestId("user").textContent).toBe("Treinador");
  });
});

// ─── logout() ────────────────────────────────────────────────────────────────

describe("logout()", () => {
  it("POST /api/auth/logout → user null → push /login", async () => {
    stubFetch([
      { ok: true, body: SESSION_USER }, // GET /api/auth/me
      { ok: true },                     // POST /api/auth/logout
    ]);

    render(<AuthProvider><Consumer /></AuthProvider>);

    await waitFor(() =>
      expect(screen.getByTestId("user").textContent).toBe("Aluno"),
    );

    fireEvent.click(screen.getByRole("button", { name: "logout" }));

    await waitFor(() =>
      expect(screen.getByTestId("user").textContent).toBe("null"),
    );

    expect(vi.mocked(fetch)).toHaveBeenCalledWith("/api/auth/logout", { method: "POST" });
    expect(mockPush).toHaveBeenCalledWith("/login");
  });
});

// ─── useAuth fora do provider ─────────────────────────────────────────────────

describe("useAuth fora do provider", () => {
  it("lança erro descritivo", () => {
    function BadConsumer() {
      useAuth();
      return null;
    }
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    expect(() => render(<BadConsumer />)).toThrow("useAuth must be used inside AuthProvider");
    spy.mockRestore();
  });
});
