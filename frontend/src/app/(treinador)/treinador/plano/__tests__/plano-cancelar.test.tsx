import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  useParams: () => ({}),
}));

vi.mock("@/lib/api/pagamento", () => ({
  pagamentoApi: {
    obterAssinaturaTreinador: vi.fn(),
    listarPlanosPlataforma: vi.fn(),
    cancelarPlanoTreinador: vi.fn(),
    trocarPlano: vi.fn(),
    obterStatusPagamentoTreinador: vi.fn(),
  },
}));

vi.mock("@/lib/api/conta", () => ({
  contaApi: { exportarDados: vi.fn() },
}));

const logoutMock = vi.fn();
vi.mock("@/lib/auth/context", () => ({
  useAuth: () => ({ logout: logoutMock }),
}));

import { pagamentoApi } from "@/lib/api/pagamento";
import { contaApi } from "@/lib/api/conta";
import PlanoTreinadorPage from "../page";

const api = vi.mocked(pagamentoApi);
const conta = vi.mocked(contaApi);

const assinaturaAtiva = {
  assinaturaTreinadorId: "as-1",
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

function mockOk() {
  api.obterAssinaturaTreinador.mockResolvedValue({ data: assinaturaAtiva } as never);
  api.listarPlanosPlataforma.mockResolvedValue({ data: planos } as never);
}

beforeEach(() => {
  vi.clearAllMocks();
  mockOk();
});

async function abrirDialogCancelar() {
  render(<PlanoTreinadorPage />);
  const botao = await screen.findByRole("button", { name: /cancelar plano/i });
  fireEvent.click(botao);
}

describe("treinador/plano — cancelamento self-service", () => {
  it("dialog mostra aviso de acesso encerrado e link Baixar meus dados", async () => {
    await abrirDialogCancelar();

    await waitFor(() => {
      expect(
        screen.getByText(/acesso será encerrado imediatamente/i),
      ).toBeInTheDocument();
    });
    expect(
      screen.getByRole("button", { name: /baixar meus dados/i }),
    ).toBeInTheDocument();
  });

  it("Baixar meus dados chama GET exportarDados", async () => {
    conta.exportarDados.mockResolvedValue({
      data: new Blob(['{"x":1}'], { type: "application/json" }),
    } as never);
    if (globalThis.URL) {
      globalThis.URL.createObjectURL = vi.fn(() => "blob:fake");
      globalThis.URL.revokeObjectURL = vi.fn();
    }
    const realCreate = document.createElement.bind(document);
    vi.spyOn(document, "createElement").mockImplementation((tag: string) => {
      const el = realCreate(tag as keyof HTMLElementTagNameMap);
      if (tag === "a") vi.spyOn(el as HTMLAnchorElement, "click").mockImplementation(vi.fn());
      return el;
    });

    await abrirDialogCancelar();
    fireEvent.click(await screen.findByRole("button", { name: /baixar meus dados/i }));

    await waitFor(() => expect(conta.exportarDados).toHaveBeenCalledOnce());
    vi.restoreAllMocks();
  });

  it("confirmar chama POST cancelarPlanoTreinador e encerra sessão", async () => {
    api.cancelarPlanoTreinador.mockResolvedValue({
      data: { canceladaEm: "2026-06-06T12:00:00Z" },
    } as never);

    await abrirDialogCancelar();
    const confirmar = await screen.findByRole("button", { name: /^confirmar cancelamento$/i });
    fireEvent.click(confirmar);

    await waitFor(() => expect(api.cancelarPlanoTreinador).toHaveBeenCalledOnce());
    await waitFor(() => expect(logoutMock).toHaveBeenCalled());
  });

  it("422 offboarding_necessario mostra mensagem específica", async () => {
    api.cancelarPlanoTreinador.mockRejectedValue({
      response: { status: 422, data: { code: "offboarding_necessario" } },
    });

    await abrirDialogCancelar();
    fireEvent.click(await screen.findByRole("button", { name: /^confirmar cancelamento$/i }));

    await waitFor(() => {
      expect(
        screen.getByText(/encerre os vínculos com seus alunos antes de cancelar o plano/i),
      ).toBeInTheDocument();
    });
    expect(logoutMock).not.toHaveBeenCalled();
  });
});
