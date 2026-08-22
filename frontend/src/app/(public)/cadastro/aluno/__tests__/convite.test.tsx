import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import CadastroAlunoPage from "../page";

vi.mock("next/link", () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  ),
}));

let query = "";
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(query),
}));

function mockFetchComConvite(resolvido: unknown, status = 200) {
  const pacotes = [
    { pacoteId: "p-1", nome: "Plano Mensal", descricao: "Acompanhamento", preco: 150, treinadorId: "t-convite" },
  ];
  vi.spyOn(global, "fetch").mockImplementation(((url: string) => {
    if (url.includes("/api/auth/convite/")) {
      return Promise.resolve({ ok: status < 400, status, json: async () => resolvido } as Response);
    }
    if (url.includes("/pacotes")) {
      return Promise.resolve({ ok: true, json: async () => pacotes } as Response);
    }
    if (url.includes("/treinadores")) {
      return Promise.resolve({ ok: true, json: async () => [] } as Response);
    }
    return Promise.resolve({ ok: true, json: async () => ({}) } as Response);
  }) as typeof fetch);
}

describe("CadastroAlunoPage — cadastro por convite", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    query = "";
  });

  it("token válido pré-preenche nome/e-mail e trava o treinador", async () => {
    query = "convite=abc123";
    mockFetchComConvite({
      nome: "Maria Silva",
      contatoTipo: "Email",
      contatoValor: "maria@example.com",
      treinadorId: "t-convite",
      treinadorNome: "Carlos Personal",
    });

    render(<CadastroAlunoPage />);

    expect(await screen.findByText("Plano Mensal")).toBeInTheDocument();
    expect(screen.getByText("Carlos Personal")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /alterar/i })).not.toBeInTheDocument();

    fireEvent.click(screen.getByText("Plano Mensal"));
    expect(screen.getByLabelText(/Nome completo/i)).toHaveValue("Maria Silva");
    expect(screen.getByLabelText(/E-mail/i)).toHaveValue("maria@example.com");
  });

  it("token válido com contato telefone pré-preenche o campo telefone, não o e-mail", async () => {
    query = "convite=xyz789";
    mockFetchComConvite({
      nome: "João Souza",
      contatoTipo: "WhatsApp",
      contatoValor: "5511987654321",
      treinadorId: "t-convite",
      treinadorNome: "Carlos Personal",
    });

    render(<CadastroAlunoPage />);
    await screen.findByText("Plano Mensal");
    fireEvent.click(screen.getByText("Plano Mensal"));

    expect(screen.getByLabelText(/Celular/i)).toHaveValue("11987654321");
    expect(screen.getByLabelText(/E-mail/i)).toHaveValue("");
  });

  it("submit com convite ativo envia conviteToken no payload", async () => {
    query = "convite=abc123";
    mockFetchComConvite({
      nome: "Maria Silva",
      contatoTipo: "Email",
      contatoValor: "maria@example.com",
      treinadorId: "t-convite",
      treinadorNome: "Carlos Personal",
    });

    render(<CadastroAlunoPage />);
    await screen.findByText("Plano Mensal");
    fireEvent.click(screen.getByText("Plano Mensal"));

    fireEvent.change(screen.getByLabelText(/Celular/i), { target: { value: "11987654321" } });
    const senhas = screen.getAllByLabelText(/senha/i);
    fireEvent.change(senhas[0], { target: { value: "Senha123abcd" } });
    fireEvent.change(senhas[1], { target: { value: "Senha123abcd" } });
    fireEvent.click(screen.getByText("Próximo"));
    await screen.findByText("Disponibilidade");

    const selectMui = async (comboIndex: number, optionRe: RegExp) => {
      const combo = screen.getAllByRole("combobox")[comboIndex];
      fireEvent.mouseDown(combo);
      const option = await screen.findByRole("option", { name: optionRe });
      fireEvent.click(option);
    };
    await selectMui(0, /3 dias/i);
    await selectMui(1, /1 hora/i);
    await selectMui(2, /^Hipertrofia$/i);
    await selectMui(3, /Iniciante/i);

    fireEvent.click(screen.getByLabelText(/dados de saúde/i));
    fireEvent.click(screen.getByRole("button", { name: /Criar conta/i }));

    await waitFor(() => {
      const registerCall = vi.mocked(global.fetch).mock.calls.find((c) => String(c[0]).includes("/register/aluno"));
      expect(registerCall).toBeDefined();
      const body = JSON.parse((registerCall![1] as RequestInit).body as string);
      expect(body.conviteToken).toBe("abc123");
      expect(body.treinadorId).toBe("t-convite");
    });
  });

  it("token inválido/expirado/consumido segue o cadastro normal sem revelar nada", async () => {
    query = "convite=token-invalido";
    mockFetchComConvite({ title: "Not Found", status: 404 }, 404);

    render(<CadastroAlunoPage />);

    expect(await screen.findByText("Carregar treinadores")).toBeInTheDocument();
    expect(screen.queryByText(/erro/i)).not.toBeInTheDocument();
  });

  it("sem query param de convite não chama o endpoint de resolução", async () => {
    query = "";
    mockFetchComConvite({});

    render(<CadastroAlunoPage />);
    await screen.findByText("Carregar treinadores");

    expect(vi.mocked(global.fetch).mock.calls.some((c) => String(c[0]).includes("/convite/"))).toBe(false);
  });
});
