import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

vi.mock("recharts", () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  PieChart: ({ children }: { children: React.ReactNode }) => <svg role="img">{children}</svg>,
  Pie: () => null,
  Cell: () => null,
  BarChart: ({ children }: { children: React.ReactNode }) => <svg role="img">{children}</svg>,
  Bar: () => null,
  XAxis: () => null,
  YAxis: () => null,
  Tooltip: () => null,
  Legend: () => null,
}));

vi.mock("@/lib/api/treinador", () => ({
  treinadorApi: {
    listVinculos: vi.fn(),
    listFichas: vi.fn(),
    listPacotes: vi.fn(),
  },
}));

vi.mock("@/lib/api/pagamento", () => ({
  pagamentoApi: {
    verificarOnboarding: vi.fn(),
    obterAssinaturaTreinador: vi.fn(),
  },
}));

import { treinadorApi } from "@/lib/api/treinador";
import { pagamentoApi } from "@/lib/api/pagamento";

const mockListVinculos = vi.mocked(treinadorApi.listVinculos);
const mockListFichas = vi.mocked(treinadorApi.listFichas);
const mockListPacotes = vi.mocked(treinadorApi.listPacotes);
const mockVerificarOnboarding = vi.mocked(pagamentoApi.verificarOnboarding);
const mockObterAssinatura = vi.mocked(pagamentoApi.obterAssinaturaTreinador);

const PACOTE = {
  pacoteId: "pac-1",
  nome: "Mensal",
  preco: 200,
  descricao: null,
};

const VINCULO_ATIVO = {
  vinculoId: "v-1",
  pacoteId: "pac-1",
  nomeAluno: "Aluno Ativo",
  emailAluno: "aluno@x.com",
};

const FICHA = {
  treinoId: "t-1",
  objetivo: "Hipertrofia",
  nome: "Ficha A",
};

function paginated<T>(items: T[], total = items.length) {
  return { data: { items, total, pagina: 1, tamanhoPagina: 100 } };
}

beforeEach(() => {
  vi.clearAllMocks();

  mockListVinculos.mockImplementation((params) => {
    const status = params?.status;
    if (status === "Ativo") {
      return Promise.resolve(paginated([VINCULO_ATIVO], 1)) as never;
    }
    return Promise.resolve(paginated([], 0)) as never;
  });
  mockListFichas.mockResolvedValue(paginated([FICHA], 1) as never);
  mockListPacotes.mockResolvedValue({ data: [PACOTE] } as never);

  mockVerificarOnboarding.mockResolvedValue({
    data: { onboardingCompleto: true, contaConfigurada: true, modoPagamentoAluno: "Plataforma", modoPagamentoPodeAlterarEm: null },
  } as never);
  mockObterAssinatura.mockResolvedValue({ data: { status: "Ativa" } } as never);
});

describe("DashboardTreinadorPage — a11y charts", () => {
  it("figure 'Alunos por status' (PieChart) tem aria-label acessível", async () => {
    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByRole("figure", { name: "Alunos por status" })).toBeInTheDocument();
    });
  });

  it("figure 'Fichas por objetivo' (BarChart) renderiza quando há fichas", async () => {
    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByRole("figure", { name: "Fichas por objetivo" })).toBeInTheDocument();
    });
  });

  it("figure 'Receita por pacote' (BarChart) renderiza quando há receita de vínculos ativos", async () => {
    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByRole("figure", { name: "Receita por pacote" })).toBeInTheDocument();
    });
  });
});
