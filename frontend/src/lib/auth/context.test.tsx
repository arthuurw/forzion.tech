import { afterEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { AuthProvider, useAuth } from "@/lib/auth/context";
import type { LoginResponse, SessionUser } from "@/types";

const mockPush = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush }),
}));

const SESSION_USER: SessionUser = {
  contaId: "c1",
  perfilId: "p1",
  tipoConta: "Aluno",
};

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

afterEach(() => {
  vi.clearAllMocks();
});

describe("AuthProvider — GET /api/auth/me", () => {
  it("sucesso (200 com user) → popula user, isLoading false", async () => {
    server.use(http.get("*/api/auth/me", () => HttpResponse.json(SESSION_USER)));
    render(
      <AuthProvider>
        <Consumer />
      </AuthProvider>,
    );

    expect(screen.getByTestId("loading")).toHaveTextContent("true");

    await waitFor(() =>
      expect(screen.getByTestId("loading")).toHaveTextContent("false"),
    );
    expect(screen.getByTestId("user")).toHaveTextContent("Aluno");
  });

  it("401 (default handler) → user null, isLoading false", async () => {
    render(
      <AuthProvider>
        <Consumer />
      </AuthProvider>,
    );

    await waitFor(() =>
      expect(screen.getByTestId("loading")).toHaveTextContent("false"),
    );
    expect(screen.getByTestId("user")).toHaveTextContent("null");
  });

  it("network error → user null, isLoading false", async () => {
    server.use(http.get("*/api/auth/me", () => HttpResponse.error()));
    render(
      <AuthProvider>
        <Consumer />
      </AuthProvider>,
    );

    await waitFor(() =>
      expect(screen.getByTestId("loading")).toHaveTextContent("false"),
    );
    expect(screen.getByTestId("user")).toHaveTextContent("null");
  });
});

describe("login()", () => {
  it("seta user com tipoConta do LoginResponse", async () => {
    render(
      <AuthProvider>
        <Consumer />
      </AuthProvider>,
    );

    await waitFor(() =>
      expect(screen.getByTestId("loading")).toHaveTextContent("false"),
    );

    fireEvent.click(screen.getByRole("button", { name: "login" }));

    expect(screen.getByTestId("user")).toHaveTextContent("Treinador");
  });
});

describe("logout()", () => {
  it("POST /api/auth/logout → user null → push /login", async () => {
    let logoutCalled = false;
    server.use(
      http.get("*/api/auth/me", () => HttpResponse.json(SESSION_USER)),
      http.post("*/api/auth/logout", () => {
        logoutCalled = true;
        return HttpResponse.json({ ok: true });
      }),
    );

    render(
      <AuthProvider>
        <Consumer />
      </AuthProvider>,
    );

    await waitFor(() =>
      expect(screen.getByTestId("user")).toHaveTextContent("Aluno"),
    );

    fireEvent.click(screen.getByRole("button", { name: "logout" }));

    await waitFor(() =>
      expect(screen.getByTestId("user")).toHaveTextContent("null"),
    );

    expect(logoutCalled).toBe(true);
    expect(mockPush).toHaveBeenCalledWith("/login");
  });
});

describe("useAuth fora do provider", () => {
  it("lanca erro descritivo", () => {
    function BadConsumer() {
      useAuth();
      return null;
    }
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    expect(() => render(<BadConsumer />)).toThrow(
      "useAuth must be used inside AuthProvider",
    );
    spy.mockRestore();
  });
});
