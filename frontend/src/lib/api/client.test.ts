import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  apiClient,
  ASSINATURA_INADIMPLENTE_EVENT,
  ASSINATURA_INADIMPLENTE_MESSAGE,
} from "./client";

// O interceptor de resposta lê `window` em tempo de chamada. Em env node não há
// window; stubamos um mínimo (location + dispatchEvent + CustomEvent) para exercer
// os ramos 401/403 sem depender de jsdom.
interface InterceptorHandler {
  fulfilled?: (res: unknown) => unknown;
  rejected?: (error: unknown) => unknown;
}

function interceptorHandlers() {
  return (apiClient.interceptors.response as unknown as { handlers: InterceptorHandler[] }).handlers;
}

function rejectedHandler() {
  const handler = interceptorHandlers().find((h) => h?.rejected)?.rejected;
  if (!handler) throw new Error("interceptor rejected handler não registrado");
  return handler;
}

function fulfilledHandler() {
  const handler = interceptorHandlers().find((h) => h?.fulfilled)?.fulfilled;
  if (!handler) throw new Error("interceptor fulfilled handler não registrado");
  return handler;
}

const fakeWindow: { location: { href: string }; dispatchEvent: ReturnType<typeof vi.fn>; __lastRequestId?: string } = {
  location: { href: "" },
  dispatchEvent: vi.fn(),
};

beforeEach(() => {
  fakeWindow.location.href = "";
  fakeWindow.__lastRequestId = undefined;
  fakeWindow.dispatchEvent.mockClear();
  vi.stubGlobal("window", fakeWindow);
  vi.stubGlobal(
    "CustomEvent",
    class {
      type: string;
      detail: unknown;
      constructor(type: string, init?: { detail?: unknown }) {
        this.type = type;
        this.detail = init?.detail;
      }
    },
  );
});

afterEach(() => vi.unstubAllGlobals());

describe("apiClient — interceptor de resposta", () => {
  it("redireciona para /login em 401", async () => {
    const handler = rejectedHandler();
    await expect(handler({ response: { status: 401 } })).rejects.toBeDefined();
    expect(fakeWindow.location.href).toBe("/login");
  });

  it("dispara evento global em 403 ASSINATURA_INADIMPLENTE", async () => {
    const handler = rejectedHandler();
    await expect(
      handler({ response: { status: 403, data: { code: "ASSINATURA_INADIMPLENTE" } } }),
    ).rejects.toBeDefined();
    expect(fakeWindow.dispatchEvent).toHaveBeenCalledTimes(1);
    const evt = fakeWindow.dispatchEvent.mock.calls[0][0] as { type: string; detail: { message: string } };
    expect(evt.type).toBe(ASSINATURA_INADIMPLENTE_EVENT);
    expect(evt.detail.message).toBe(ASSINATURA_INADIMPLENTE_MESSAGE);
    expect(fakeWindow.location.href).toBe("");
  });

  it("403 sem code não dispara evento", async () => {
    const handler = rejectedHandler();
    await expect(handler({ response: { status: 403, data: {} } })).rejects.toBeDefined();
    expect(fakeWindow.dispatchEvent).not.toHaveBeenCalled();
  });

  it("propaga outros erros sem efeito colateral", async () => {
    const handler = rejectedHandler();
    await expect(handler({ response: { status: 500 } })).rejects.toBeDefined();
    expect(fakeWindow.location.href).toBe("");
    expect(fakeWindow.dispatchEvent).not.toHaveBeenCalled();
  });

  it("erro sem response (rede) é propagado", async () => {
    const handler = rejectedHandler();
    await expect(handler({ message: "Network Error" })).rejects.toBeDefined();
  });

  // FE-01: correlação Sentry sem id stale.
  it("grava __lastRequestId quando a resposta traz X-Request-Id", () => {
    fulfilledHandler()({ headers: { "x-request-id": "req-123" } });
    expect(fakeWindow.__lastRequestId).toBe("req-123");
  });

  it("limpa __lastRequestId em resposta 204 sem X-Request-Id", () => {
    fakeWindow.__lastRequestId = "req-antigo";
    fulfilledHandler()({ headers: {} });
    expect(fakeWindow.__lastRequestId).toBeUndefined();
  });
});
