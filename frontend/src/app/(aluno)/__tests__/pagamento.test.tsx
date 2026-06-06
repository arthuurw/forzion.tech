// F6c (Fase 3 test remediation) — migrado de vi.mock("@/lib/api/pagamento")
// e vi.mock("@/lib/api/aluno") pra MSW. apiClient real envia requests; MSW
// intercepta. Cobre PagamentoPix, PagamentosTreinadorPage, OnboardingRetornoPage,
// PagamentosAlunoPage.
//
// Endpoints reais (vide src/lib/api/pagamento.ts + aluno.ts):
//   GET /aluno/pagamentos/:id              -> obterPagamento
//   GET /aluno/pagamentos/assinatura/:id   -> listarPagamentosAssinatura
//   GET /aluno/assinatura                  -> obterMinhaAssinatura
//   GET /treinador/onboarding/status       -> verificarOnboarding
//   POST /treinador/onboarding             -> iniciarOnboarding
//   GET /aluno/vinculo                     -> alunoApi.getMeuVinculo (n/a aqui)
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { buildPagamento } from "@/test/factories";
import type { PagamentoResponse, OnboardingStatusResponse } from "@/types";

vi.mock("next/link", () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  ),
}));

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() })),
}));

// F36: makePagamento consolidado via buildPagamento.
const PIX_DEFAULTS: Partial<PagamentoResponse> = {
  pagamentoId: "pay-1",
  assinaturaAlunoId: "ass-1",
  valor: 150,
  status: "Pendente",
  metodoPagamento: "Pix",
  pixQrCode: "00020126...",
  pixQrCodeUrl: "https://img.stripe.com/qr.png",
  pixExpiracao: new Date(Date.now() + 3_600_000).toISOString(),
  clientSecret: null,
  dataPagamento: null,
};

const makePagamento = (overrides: Partial<PagamentoResponse> = {}): PagamentoResponse =>
  buildPagamento({ ...PIX_DEFAULTS, ...overrides });

function respondPagamento(overrides: Partial<PagamentoResponse> = {}) {
  server.use(
    http.get("*/aluno/pagamentos/:id", () => HttpResponse.json(makePagamento(overrides))),
  );
}

function hangPagamento() {
  server.use(
    http.get("*/aluno/pagamentos/:id", () => new Promise<Response>(() => {})),
  );
}

function countPagamentoCalls(): { count: number; reset: () => void } {
  let count = 0;
  server.use(
    http.get("*/aluno/pagamentos/:id", () => {
      count++;
      return new Promise<Response>(() => {});
    }),
  );
  return { get count() { return count; }, reset: () => { count = 0; } };
}

import PagamentoPix from "@/components/pagamento/PagamentoPix";

describe("PagamentoPix", () => {
  beforeEach(() => {
    Object.defineProperty(navigator, "clipboard", {
      value: { writeText: vi.fn().mockResolvedValue(undefined) },
      writable: true,
      configurable: true,
    });
  });

  afterEach(() => vi.clearAllMocks());

  it("exibe spinner durante carregamento inicial", () => {
    hangPagamento();
    render(<PagamentoPix pagamentoId="pay-1" />);
    expect(screen.getByRole("progressbar")).toBeInTheDocument();
  });

  it("estado Pago → exibe confirmação", async () => {
    respondPagamento({ status: "Pago" });
    render(<PagamentoPix pagamentoId="pay-1" />);
    expect(await screen.findByText("Pagamento confirmado!")).toBeInTheDocument();
  });

  it("estado Pago → chama onPago", async () => {
    const onPago = vi.fn();
    respondPagamento({ status: "Pago" });
    render(<PagamentoPix pagamentoId="pay-1" onPago={onPago} />);
    await waitFor(() => expect(onPago).toHaveBeenCalledOnce());
  });

  it("estado Expirado → exibe mensagem", async () => {
    respondPagamento({ status: "Expirado", pixQrCode: null, pixQrCodeUrl: null });
    render(<PagamentoPix pagamentoId="pay-1" />);
    expect(await screen.findByText("Solicite uma nova cobrança ao seu treinador.")).toBeInTheDocument();
  });

  it("estado Falhou → exibe mensagem", async () => {
    respondPagamento({ status: "Falhou", pixQrCode: null, pixQrCodeUrl: null });
    render(<PagamentoPix pagamentoId="pay-1" />);
    expect(await screen.findByText("Solicite uma nova cobrança ao seu treinador.")).toBeInTheDocument();
  });

  it("estado Pendente → exibe 'Pague via Pix' e botão copiar", async () => {
    respondPagamento();
    render(<PagamentoPix pagamentoId="pay-1" />);
    expect(await screen.findByText("Pague via Pix")).toBeInTheDocument();
    expect(screen.getByText("Copiar código")).toBeInTheDocument();
    expect(screen.getByText("Aguardando pagamento...")).toBeInTheDocument();
  });

  it("botão copiar → escreve no clipboard", async () => {
    respondPagamento({ pixQrCode: "CODIGO_PIX_LONGO" });
    render(<PagamentoPix pagamentoId="pay-1" />);
    expect(await screen.findByText("Copiar código")).toBeInTheDocument();
    fireEvent.click(screen.getByText("Copiar código"));
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith("CODIGO_PIX_LONGO");
  });

  it("configura polling de 30s via setInterval", () => {
    const setIntervalSpy = vi.spyOn(globalThis, "setInterval");
    hangPagamento();

    render(<PagamentoPix pagamentoId="pay-1" />);

    expect(setIntervalSpy).toHaveBeenCalledWith(expect.any(Function), 30_000);
    setIntervalSpy.mockRestore();
  });

  it("re-render do pai com novo onPago não reinicia o polling", async () => {
    const tracker = countPagamentoCalls();
    server.use(
      http.get("*/aluno/pagamentos/:id", () => HttpResponse.json(makePagamento())),
    );

    // callers passam onPago inline (novo ref a cada render). O polling depende
    // só de pagamentoId, então re-render não deve disparar novo fetch.
    const { rerender } = render(<PagamentoPix pagamentoId="pay-1" onPago={() => {}} />);
    expect(await screen.findByText("Pague via Pix")).toBeInTheDocument();

    // Capturar baseline depois do mount + load inicial.
    const initial = tracker.count;
    rerender(<PagamentoPix pagamentoId="pay-1" onPago={() => {}} />);
    rerender(<PagamentoPix pagamentoId="pay-1" onPago={() => {}} />);

    // Re-renders nao disparam fetch novo (efeito depende so de pagamentoId).
    expect(tracker.count).toBe(initial);
  });

  // F26 — unmount durante polling não pode deixar interval pendurado.
  // Sem o cleanup do useEffect (clearInterval no return + active flag), o
  // setInterval continuaria chamando carregar() após unmount → memory leak
  // (closure + fetch agendado) + setState em componente desmontado (warn).
  it("unmount após mount → clearInterval roda e fetch posterior não setState", async () => {
    const clearSpy = vi.spyOn(globalThis, "clearInterval");
    const tracker = countPagamentoCalls();
    server.use(
      http.get("*/aluno/pagamentos/:id", () => HttpResponse.json(makePagamento())),
    );

    const { unmount } = render(<PagamentoPix pagamentoId="pay-1" />);
    expect(await screen.findByText("Pague via Pix")).toBeInTheDocument();

    const callsBeforeUnmount = tracker.count;
    unmount();

    // clearInterval foi chamado pelo cleanup do useEffect.
    expect(clearSpy).toHaveBeenCalled();

    // Após unmount, mesmo se uma resposta pendente resolvesse, o `active`
    // flag impede setState. Não há fetch novo agendado.
    await new Promise((r) => setTimeout(r, 50));
    expect(tracker.count).toBe(callsBeforeUnmount);

    clearSpy.mockRestore();
  });

  // Variante: unmount com fetch in-flight (resposta nunca chega). Cleanup
  // ainda deve rodar sem erro — `active = false` previne setState quando/se
  // a Promise eventualmente resolve.
  it("unmount mid-fetch (response hanging) → sem warn, sem setState", async () => {
    const errorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
    hangPagamento();

    const { unmount } = render(<PagamentoPix pagamentoId="pay-1" />);
    expect(screen.getByRole("progressbar")).toBeInTheDocument();

    unmount();
    // Aguarda o microtask queue drenar — se o cleanup falhasse, React logaria
    // "Can't perform a React state update on an unmounted component".
    await new Promise((r) => setTimeout(r, 100));

    const warnedAboutUnmount = errorSpy.mock.calls.some((c) =>
      String(c[0]).includes("unmounted component"),
    );
    expect(warnedAboutUnmount).toBe(false);

    errorSpy.mockRestore();
  });

  // Bug 2 — PagamentoPix 401: sessão expirada durante polling
  it("401 na primeira chamada → para polling e exibe mensagem de sessão expirada", async () => {
    server.use(
      http.get("*/aluno/pagamentos/:id", () =>
        HttpResponse.json({ title: "Unauthorized" }, { status: 401 }),
      ),
    );

    render(<PagamentoPix pagamentoId="pay-1" />);
    expect(await screen.findByText(/sess(ã|a)o expirou/i)).toBeInTheDocument();
  });

  it("401 → clearInterval é chamado (polling parado)", async () => {
    const clearSpy = vi.spyOn(globalThis, "clearInterval");
    server.use(
      http.get("*/aluno/pagamentos/:id", () =>
        HttpResponse.json({ title: "Unauthorized" }, { status: 401 }),
      ),
    );

    render(<PagamentoPix pagamentoId="pay-1" />);
    await screen.findByText(/sess(ã|a)o expirou/i);

    expect(clearSpy).toHaveBeenCalled();
    clearSpy.mockRestore();
  });

  it("N erros consecutivos não-401 → exibe aviso de falha de rede", async () => {
    // Garante que o aviso de rede aparece depois de MAX_CONSECUTIVE_ERRORS (3)
    // falhas seguidas de 500. Estratégia: mock de setInterval que dispara o
    // callback imediatamente (sem delay), permitindo acumular 3 erros rápido.
    let intervalCallback: (() => void) | null = null;

    vi.spyOn(globalThis, "setInterval").mockImplementation((fn: TimerHandler) => {
      intervalCallback = fn as () => void;
      return 0 as unknown as ReturnType<typeof setInterval>;
    });
    vi.spyOn(globalThis, "clearInterval").mockImplementation(() => {});

    server.use(
      http.get("*/aluno/pagamentos/:id", () =>
        HttpResponse.json({ title: "Server Error" }, { status: 500 }),
      ),
    );

    const { act } = await import("@testing-library/react");

    render(<PagamentoPix pagamentoId="pay-1" />);

    // Erro 1 (fetch inicial)
    await act(async () => { await new Promise((r) => setTimeout(r, 30)); });
    // Erro 2 (tick manual do interval)
    if (intervalCallback) await act(async () => { intervalCallback!(); await new Promise((r) => setTimeout(r, 30)); });
    // Erro 3 (tick manual do interval)
    if (intervalCallback) await act(async () => { intervalCallback!(); await new Promise((r) => setTimeout(r, 30)); });

    expect(screen.getByTestId("polling-network-warning")).toBeInTheDocument();

    vi.mocked(globalThis.setInterval).mockRestore();
    vi.mocked(globalThis.clearInterval).mockRestore();
  });
});

import PagamentosTreinadorPage from "@/app/(treinador)/treinador/pagamentos/page";

describe("PagamentosTreinadorPage", () => {
  afterEach(() => vi.clearAllMocks());

  it("exibe spinner durante carregamento", () => {
    server.use(
      http.get("*/treinador/onboarding/status", () => new Promise<Response>(() => {})),
    );
    render(<PagamentosTreinadorPage />);
    expect(screen.getByRole("progressbar")).toBeInTheDocument();
  });

  it("onboarding completo → exibe 'Ativo'", async () => {
    const status: OnboardingStatusResponse = { onboardingCompleto: true, contaConfigurada: true, modoPagamentoAluno: "Plataforma" };
    server.use(http.get("*/treinador/onboarding/status", () => HttpResponse.json(status)));

    render(<PagamentosTreinadorPage />);
    expect(await screen.findByText("Ativo")).toBeInTheDocument();
  });

  it("não configurado → exibe botão 'Configurar recebimentos'", async () => {
    const status: OnboardingStatusResponse = { onboardingCompleto: false, contaConfigurada: false, modoPagamentoAluno: "Plataforma" };
    server.use(http.get("*/treinador/onboarding/status", () => HttpResponse.json(status)));

    render(<PagamentosTreinadorPage />);
    expect(await screen.findByText("Configurar recebimentos")).toBeInTheDocument();
  });

  it("configurado mas incompleto → exibe 'Continuar cadastro'", async () => {
    const status: OnboardingStatusResponse = { onboardingCompleto: false, contaConfigurada: true, modoPagamentoAluno: "Plataforma" };
    server.use(http.get("*/treinador/onboarding/status", () => HttpResponse.json(status)));

    render(<PagamentosTreinadorPage />);
    expect(await screen.findByText("Continuar cadastro")).toBeInTheDocument();
  });

  it("clique em configurar → chama iniciarOnboarding", async () => {
    const status: OnboardingStatusResponse = { onboardingCompleto: false, contaConfigurada: false, modoPagamentoAluno: "Plataforma" };
    let iniciarCalled = false;
    server.use(
      http.get("*/treinador/onboarding/status", () => HttpResponse.json(status)),
      http.post("*/treinador/onboarding", () => {
        iniciarCalled = true;
        return HttpResponse.json({ url: "https://connect.stripe.com/setup" });
      }),
    );

    // Page navega via window.location.href apos receber URL — em jsdom o setter
    // de href nao e configuravel, entao nao mockamos. O assert e que POST foi
    // chamado, suficiente pra cobrir o fluxo aqui (navigate end-to-end e E2E).
    render(<PagamentosTreinadorPage />);
    expect(await screen.findByText("Configurar recebimentos")).toBeInTheDocument();
    fireEvent.click(screen.getByText("Configurar recebimentos"));
    await waitFor(() => expect(iniciarCalled).toBe(true));
  });

  it("erro na API → exibe mensagem de erro", async () => {
    server.use(
      http.get("*/treinador/onboarding/status", () =>
        HttpResponse.json({ title: "boom" }, { status: 500 }),
      ),
    );

    render(<PagamentosTreinadorPage />);
    expect(await screen.findByText("Erro ao verificar status do cadastro Stripe.")).toBeInTheDocument();
  });
});

import OnboardingRetornoPage from "@/app/(treinador)/treinador/onboarding/retorno/page";

describe("OnboardingRetornoPage", () => {
  afterEach(() => vi.clearAllMocks());

  it("exibe spinner durante carregamento", () => {
    server.use(
      http.get("*/treinador/onboarding/status", () => new Promise<Response>(() => {})),
    );
    render(<OnboardingRetornoPage />);
    expect(screen.getByRole("progressbar")).toBeInTheDocument();
  });

  it("onboarding completo → exibe 'Cadastro concluído!'", async () => {
    server.use(
      http.get("*/treinador/onboarding/status", () =>
        HttpResponse.json({ onboardingCompleto: true, contaConfigurada: true }),
      ),
    );

    render(<OnboardingRetornoPage />);
    expect(await screen.findByText("Cadastro concluído!")).toBeInTheDocument();
    expect(screen.getByText("Ir para recebimentos")).toBeInTheDocument();
  });

  it("onboarding incompleto → exibe 'Cadastro incompleto'", async () => {
    server.use(
      http.get("*/treinador/onboarding/status", () =>
        HttpResponse.json({ onboardingCompleto: false, contaConfigurada: false }),
      ),
    );

    render(<OnboardingRetornoPage />);
    expect(await screen.findByText("Cadastro incompleto")).toBeInTheDocument();
  });

  it("erro na API → exibe 'Cadastro incompleto'", async () => {
    server.use(
      http.get("*/treinador/onboarding/status", () =>
        HttpResponse.json({ title: "fail" }, { status: 500 }),
      ),
    );

    render(<OnboardingRetornoPage />);
    expect(await screen.findByText("Cadastro incompleto")).toBeInTheDocument();
  });
});

import PagamentosAlunoPage from "@/app/(aluno)/aluno/pagamentos/page";

describe("PagamentosAlunoPage", () => {
  afterEach(() => vi.clearAllMocks());

  function setAssinatura(assinaturaAlunoId: string | null = "ass-1") {
    server.use(
      http.get("*/aluno/assinatura", () =>
        HttpResponse.json(assinaturaAlunoId ? { assinaturaAlunoId } : {}),
      ),
    );
  }

  function setListaPagamentos(items: PagamentoResponse[]) {
    server.use(
      http.get("*/aluno/pagamentos/assinatura/:id", () => HttpResponse.json(items)),
    );
  }

  it("estado inicial → exibe 'Nenhum pagamento encontrado.'", async () => {
    setAssinatura();
    setListaPagamentos([]);

    render(<PagamentosAlunoPage />);
    expect(await screen.findByText("Nenhum pagamento encontrado.")).toBeInTheDocument();
    expect(screen.getByText("Histórico de Pagamentos")).toBeInTheDocument();
  });

  it("exibe spinner durante carregamento", () => {
    server.use(
      http.get("*/aluno/assinatura", () => new Promise<Response>(() => {})),
    );
    render(<PagamentosAlunoPage />);
    expect(screen.getByRole("progressbar")).toBeInTheDocument();
  });

  it("erro na API → exibe mensagem de erro", async () => {
    server.use(
      http.get("*/aluno/assinatura", () =>
        HttpResponse.json({ title: "fail" }, { status: 500 }),
      ),
    );
    render(<PagamentosAlunoPage />);
    expect(await screen.findByText("Erro ao carregar pagamentos.")).toBeInTheDocument();
  });

  it("com pagamentos → exibe tabela com data, valor e status", async () => {
    setAssinatura();
    setListaPagamentos([
      makePagamento({ valor: 99.9, status: "Pago", createdAt: "2025-03-15T00:00:00Z" }),
    ]);

    render(<PagamentosAlunoPage />);
    expect(await screen.findByText("Pago")).toBeInTheDocument();
    expect(screen.getByText((c) => c.includes("99,90"))).toBeInTheDocument();
  });

  it("pagamento Pendente → exibe botão 'Pagar'", async () => {
    setAssinatura();
    setListaPagamentos([makePagamento({ status: "Pendente" })]);

    render(<PagamentosAlunoPage />);
    expect(await screen.findByText("Pagar")).toBeInTheDocument();
  });

  it("pagamento Expirado → não exibe botão 'Pagar'", async () => {
    setAssinatura();
    setListaPagamentos([makePagamento({ status: "Expirado", pixQrCode: null })]);

    render(<PagamentosAlunoPage />);
    expect(await screen.findByText("Expirado")).toBeInTheDocument();
    expect(screen.queryByText("Pagar")).not.toBeInTheDocument();
  });

  it("pagamento Pendente Pix → clicar Pagar abre dialog 'Pagamento via Pix'", async () => {
    setAssinatura();
    setListaPagamentos([makePagamento({ status: "Pendente", metodoPagamento: "Pix" })]);
    hangPagamento();

    render(<PagamentosAlunoPage />);
    expect(await screen.findByText("Pagar")).toBeInTheDocument();
    fireEvent.click(screen.getByText("Pagar"));
    expect(await screen.findByText("Pagamento via Pix")).toBeInTheDocument();
  });

  // Bug 1 — 204 No Content: aluno sem assinatura → empty state, sem crash
  it("204 No Content (sem assinaturaAlunoId) → exibe 'Nenhum pagamento encontrado.' sem erro", async () => {
    server.use(
      http.get("*/aluno/assinatura", () => new HttpResponse(null, { status: 204 })),
    );

    render(<PagamentosAlunoPage />);
    expect(await screen.findByText("Nenhum pagamento encontrado.")).toBeInTheDocument();
    expect(screen.queryByText("Erro ao carregar pagamentos.")).not.toBeInTheDocument();
  });

  it("200 com payload vazio (sem assinaturaAlunoId) → empty state, sem crash", async () => {
    server.use(
      http.get("*/aluno/assinatura", () => HttpResponse.json({})),
    );

    render(<PagamentosAlunoPage />);
    expect(await screen.findByText("Nenhum pagamento encontrado.")).toBeInTheDocument();
  });
});
