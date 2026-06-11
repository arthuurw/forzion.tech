import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { authApi, AuthApiError } from "./auth";

function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  } as Response;
}

const fetchMock = vi.fn();

beforeEach(() => {
  vi.stubGlobal("fetch", fetchMock);
  fetchMock.mockReset();
});

afterEach(() => vi.unstubAllGlobals());

describe("authApi", () => {
  it("login POSTs to /api/auth with email/senha and returns parsed body", async () => {
    const payload = { tipoConta: "Aluno" };
    fetchMock.mockResolvedValueOnce(jsonResponse(payload));

    const result = await authApi.login({ email: "a@b.com", senha: "x" });

    expect(result).toEqual(payload);
    expect(fetchMock).toHaveBeenCalledWith("/api/auth", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: "a@b.com", senha: "x" }),
    });
  });

  it("throws AuthApiError carrying status and parsed ProblemDetails on non-2xx", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ title: "t", code: "EMAIL_NAO_VERIFICADO" }, 403));

    const err = await authApi.login({ email: "a@b.com", senha: "x" }).catch((e) => e);

    expect(err).toBeInstanceOf(AuthApiError);
    expect(err.status).toBe(403);
    expect(err.problem?.code).toBe("EMAIL_NAO_VERIFICADO");
  });

  it("AuthApiError has null problem when error body is not JSON", async () => {
    fetchMock.mockResolvedValueOnce({
      ok: false,
      status: 500,
      json: async () => {
        throw new Error("not json");
      },
    } as unknown as Response);

    const err = await authApi.login({ email: "a@b.com", senha: "x" }).catch((e) => e);

    expect(err).toBeInstanceOf(AuthApiError);
    expect(err.status).toBe(500);
    expect(err.problem).toBeNull();
  });

  it("resendVerification POSTs the email", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({}));
    await authApi.resendVerification("a@b.com");
    expect(fetchMock).toHaveBeenCalledWith("/api/auth/resend-verification", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: "a@b.com" }),
    });
  });

  it("listarTreinadores GETs the route", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse([{ treinadorId: "t1" }]));
    const result = await authApi.listarTreinadores();
    expect(result).toEqual([{ treinadorId: "t1" }]);
    expect(fetchMock).toHaveBeenCalledWith("/api/auth/treinadores");
  });

  it("listarPacotes GETs the nested route", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse([]));
    await authApi.listarPacotes("t1");
    expect(fetchMock).toHaveBeenCalledWith("/api/auth/treinadores/t1/pacotes");
  });

  it("listarPlanos GETs the planos route", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse([]));
    await authApi.listarPlanos();
    expect(fetchMock).toHaveBeenCalledWith("/api/auth/planos");
  });

  it("registerAluno POSTs to register/aluno", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({}));
    const body = { nome: "N" } as Parameters<typeof authApi.registerAluno>[0];
    await authApi.registerAluno(body);
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/auth/register/aluno",
      expect.objectContaining({ method: "POST", body: JSON.stringify(body) }),
    );
  });

  it("registerTreinador POSTs to register/treinador and returns body", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ treinadorId: "t1", status: "AguardandoPagamento" }));
    const body = { nome: "N" } as Parameters<typeof authApi.registerTreinador>[0];
    const result = await authApi.registerTreinador(body);
    expect(result).toEqual({ treinadorId: "t1", status: "AguardandoPagamento" });
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/auth/register/treinador",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("iniciarPagamentoTreinador POSTs metodo to the pagamento route", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ metodoPagamento: "Pix" }));
    await authApi.iniciarPagamentoTreinador("t1", "Pix");
    expect(fetchMock).toHaveBeenCalledWith("/api/auth/treinador/t1/pagamento", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ metodo: "Pix" }),
    });
  });
});
