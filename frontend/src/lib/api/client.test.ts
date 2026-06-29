import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  apiClient,
  ASSINATURA_INADIMPLENTE_EVENT,
  ASSINATURA_INADIMPLENTE_MESSAGE,
} from "./client";
import { registerStepUpHandler } from "@/lib/auth/stepUpController";

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

const originalAdapter = apiClient.defaults.adapter;
afterEach(() => {
  vi.unstubAllGlobals();
  apiClient.defaults.adapter = originalAdapter;
});

// Adapter fake: resolve toda request sem rede (usado p/ exercer o retry pós-refresh).
function stubAdapter() {
  const adapter = vi.fn(async (config: unknown) => ({
    data: { retried: true },
    status: 200,
    statusText: "OK",
    headers: {},
    config,
  }));
  apiClient.defaults.adapter = adapter as never;
  return adapter;
}

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

describe("apiClient — renovação silenciosa em 401", () => {
  it("401 com config → refresh + refaz a request original (sem deslogar)", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(null, { status: 200 })));
    const adapter = stubAdapter();

    const result = (await rejectedHandler()({
      response: { status: 401 },
      config: { url: "/alunos", headers: {} },
    })) as { data: unknown };

    expect(result.data).toEqual({ retried: true });
    expect(adapter).toHaveBeenCalledTimes(1);
    expect(fakeWindow.location.href).toBe("");
  });

  it("401 concorrentes coalescem em 1 só refresh (anti-tempestade)", async () => {
    let resolveFetch!: (r: Response) => void;
    const fetchMock = vi.fn(() => new Promise<Response>((r) => (resolveFetch = r)));
    vi.stubGlobal("fetch", fetchMock);
    stubAdapter();
    const handler = rejectedHandler();

    const p1 = handler({ response: { status: 401 }, config: { url: "/a", headers: {} } });
    const p2 = handler({ response: { status: 401 }, config: { url: "/b", headers: {} } });
    resolveFetch(new Response(null, { status: 200 }));
    await Promise.all([p1, p2]);

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("refresh falha → redireciona /login", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(null, { status: 401 })));
    const handler = rejectedHandler();

    await expect(
      handler({ response: { status: 401 }, config: { url: "/a", headers: {} } }),
    ).rejects.toBeDefined();
    expect(fakeWindow.location.href).toBe("/login");
  });

  it("401 já retried → não refaz refresh, vai pro /login (sem loop)", async () => {
    const fetchMock = vi.fn(async () => new Response(null, { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    const handler = rejectedHandler();

    await expect(
      handler({ response: { status: 401 }, config: { url: "/a", _retry: true, headers: {} } }),
    ).rejects.toBeDefined();
    expect(fetchMock).not.toHaveBeenCalled();
    expect(fakeWindow.location.href).toBe("/login");
  });
});

describe("apiClient — step-up em 403 step_up_requerido", () => {
  it("solicita step-up e refaz a request com o header X-Step-Up-Token", async () => {
    const unregister = registerStepUpHandler(async () => "su-tok");
    const adapter = stubAdapter();
    const setHeader = vi.fn();

    const result = (await rejectedHandler()({
      response: { status: 403, data: { code: "step_up_requerido" } },
      config: { url: "/conta/email/trocar", headers: { set: setHeader } },
    })) as { data: unknown };

    expect(setHeader).toHaveBeenCalledWith("X-Step-Up-Token", "su-tok");
    expect(result.data).toEqual({ retried: true });
    expect(adapter).toHaveBeenCalledTimes(1);
    unregister();
  });

  it("step-up cancelado (sem token) → propaga erro sem refazer", async () => {
    const unregister = registerStepUpHandler(async () => null);
    const adapter = stubAdapter();

    await expect(
      rejectedHandler()({
        response: { status: 403, data: { code: "step_up_requerido" } },
        config: { url: "/conta/email/trocar", headers: { set: vi.fn() } },
      }),
    ).rejects.toBeDefined();
    expect(adapter).not.toHaveBeenCalled();
    unregister();
  });

  it("já tentou step-up (_stepUpRetry) → não repete o desafio", async () => {
    const handlerFn = vi.fn(async () => "su-tok");
    const unregister = registerStepUpHandler(handlerFn);
    const adapter = stubAdapter();

    await expect(
      rejectedHandler()({
        response: { status: 403, data: { code: "step_up_requerido" } },
        config: { url: "/conta/email/trocar", _stepUpRetry: true, headers: { set: vi.fn() } },
      }),
    ).rejects.toBeDefined();
    expect(handlerFn).not.toHaveBeenCalled();
    expect(adapter).not.toHaveBeenCalled();
    unregister();
  });
});
