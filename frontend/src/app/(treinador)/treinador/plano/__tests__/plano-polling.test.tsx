import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  useParams: () => ({}),
}));

vi.mock("@/lib/api/pagamento", () => ({
  pagamentoApi: {
    obterAssinaturaTreinador: vi.fn(),
    listarPlanosPlataforma: vi.fn(),
    trocarPlano: vi.fn(),
    obterStatusPagamentoTreinador: vi.fn(),
    cancelarPlanoTreinador: vi.fn(),
  },
}));

vi.mock("@/lib/api/conta", () => ({
  contaApi: { exportarDados: vi.fn() },
}));

vi.mock("@/lib/auth/context", () => ({
  useAuth: () => ({ logout: vi.fn() }),
}));

import { pagamentoApi } from "@/lib/api/pagamento";
import PlanoTreinadorPage from "../page";

const api = vi.mocked(pagamentoApi);

const assinaturaAtiva = {
  planoPlataformaId: "plano-pro",
  planoPlataformaIdAgendado: null,
  status: "Ativa",
  valor: 99.9,
  dataProximaCobranca: "2026-07-06T00:00:00Z",
};

const planos = [
  { planoId: "plano-pro", nome: "Pro", preco: 99.9, maxAlunos: 50, tier: "Pro", isAtivo: true },
  { planoId: "plano-start", nome: "Start", preco: 49.9, maxAlunos: 10, tier: "Start", isAtivo: true },
];

const upgradePixResp = {
  tipo: "Upgrade",
  pagamentoId: "pay-pix-123",
  valorPagamento: 50,
  metodoPagamento: "Pix",
  pixQrCode: "00020126fake",
  pixQrCodeUrl: null,
  pixExpiracao: null,
  clientSecret: null,
  dataEfetivacao: null,
};

function mockApiOk() {
  api.obterAssinaturaTreinador.mockResolvedValue({ data: assinaturaAtiva } as never);
  api.listarPlanosPlataforma.mockResolvedValue({ data: planos } as never);
  api.obterStatusPagamentoTreinador.mockResolvedValue({ data: { status: "Pendente" } } as never);
  api.trocarPlano.mockResolvedValue({ data: upgradePixResp } as never);
}

describe("plano polling — backoff + visibility (R3a)", () => {
  // Captured [callback, delay] pairs for setTimeout calls with delay >= 1000ms.
  // Delays below 1000ms are passed through (waitFor retries at 50ms must not be captured).
  let capturedTimeouts: [() => void, number][] = [];
  let setTimeoutSpy: ReturnType<typeof vi.spyOn> | null = null;
  const realSetTimeout = globalThis.setTimeout.bind(globalThis);

  beforeEach(() => {
    vi.clearAllMocks();
    mockApiOk();
    capturedTimeouts = [];
    setTimeoutSpy = null;
  });

  afterEach(() => {
    setTimeoutSpy?.mockRestore();
    vi.restoreAllMocks();
  });

  function installSpy() {
    setTimeoutSpy = vi.spyOn(globalThis, "setTimeout").mockImplementation(
      (fn: TimerHandler, delay?: number) => {
        const d = delay ?? 0;
        if (d >= 5_000) {
          capturedTimeouts.push([fn as () => void, d]);
          return 99 as unknown as ReturnType<typeof setTimeout>;
        }
        return realSetTimeout(fn, d) as unknown as ReturnType<typeof setTimeout>;
      },
    );
  }

  // Reaches the "pagando" state and waits until iniciarPolling registers its first
  // timeout. The spy is installed AFTER page load so that waitFor's internal
  // retries (50ms) and initial load work with real timers.
  async function triggerPolling() {
    const botoesTocar = await screen.findAllByRole("button", { name: /trocar/i });

    installSpy();

    fireEvent.click(botoesTocar[0]);
    await screen.findByRole("button", { name: /^confirmar$/i });

    fireEvent.click(screen.getByRole("button", { name: /^confirmar$/i }));

    // real timer aqui: os timeouts capturados (não-disparados) pelo spy travariam um fake timer
    await act(async () => {
      await new Promise<void>((r) => realSetTimeout(r, 50));
    });

    expect(capturedTimeouts).toHaveLength(1);
  }

  it("primeiro tick agendado com delay 5s", async () => {
    render(<PlanoTreinadorPage />);
    await triggerPolling();
    expect(capturedTimeouts[0][1]).toBe(5_000);
  });

  it("após tick retornar Pendente, próximo timeout com delay 10s (backoff)", async () => {
    render(<PlanoTreinadorPage />);
    await triggerPolling();

    await act(async () => { await capturedTimeouts[0][0](); });

    expect(api.obterStatusPagamentoTreinador).toHaveBeenCalledTimes(1);
    expect(capturedTimeouts[1][1]).toBe(10_000);
  });

  it("delays crescem: 5→10→20→cap30", async () => {
    render(<PlanoTreinadorPage />);
    await triggerPolling();

    for (let i = 0; i < 4; i++) {
      await act(async () => { await capturedTimeouts[i][0](); });
    }

    expect(capturedTimeouts[0][1]).toBe(5_000);
    expect(capturedTimeouts[1][1]).toBe(10_000);
    expect(capturedTimeouts[2][1]).toBe(20_000);
    expect(capturedTimeouts[3][1]).toBe(30_000);
    expect(capturedTimeouts[4][1]).toBe(30_000);
  });

  it("para após deadline e exibe aviso de expiração", async () => {
    render(<PlanoTreinadorPage />);

    const fakeNow = Date.now();
    const nowSpy = vi.spyOn(Date, "now").mockReturnValue(fakeNow);

    await triggerPolling();
    nowSpy.mockReturnValue(fakeNow + 180_001);

    await act(async () => { await capturedTimeouts[0][0](); });

    expect(screen.getByText(/verificação expirou/i)).toBeInTheDocument();
    expect(capturedTimeouts).toHaveLength(1);
    nowSpy.mockRestore();
  });

  it("document.hidden=true → tick reagenda sem chamar obterStatusPagamentoTreinador", async () => {
    render(<PlanoTreinadorPage />);
    await triggerPolling();

    Object.defineProperty(document, "hidden", { value: true, writable: true, configurable: true });
    try {
      await act(async () => { await capturedTimeouts[0][0](); });
      expect(api.obterStatusPagamentoTreinador).not.toHaveBeenCalled();
      expect(capturedTimeouts).toHaveLength(2);
      expect(capturedTimeouts[1][1]).toBe(5_000);
    } finally {
      Object.defineProperty(document, "hidden", { value: false, writable: true, configurable: true });
    }
  });

  it("status Pago → para polling (sem novo timeout) e transita para sucesso", async () => {
    api.obterStatusPagamentoTreinador.mockResolvedValue({ data: { status: "Pago" } } as never);
    render(<PlanoTreinadorPage />);
    await triggerPolling();

    await act(async () => { await capturedTimeouts[0][0](); });

    expect(screen.getByText(/plano atualizado/i)).toBeInTheDocument();
    expect(capturedTimeouts).toHaveLength(1);
  });
});
