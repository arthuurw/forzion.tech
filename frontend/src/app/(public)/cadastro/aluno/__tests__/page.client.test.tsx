import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import CadastroAlunoPage from "../page";

vi.mock("next/link", () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  ),
}));

describe("CadastroAlunoPage (R6 resumo + R8 consentimento anamnese)", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  const fillUntilAnamnese = async () => {
    const treinadores = [{ treinadorId: "t-1", nome: "Carlos" }];
    const pacotes = [
      { pacoteId: "p-1", nome: "Plano Mensal", descricao: "Acompanhamento", preco: 150, treinadorId: "t-1" },
    ];
    vi.spyOn(global, "fetch").mockImplementation(((url: string) => {
      if (url.includes("/pacotes")) {
        return Promise.resolve({ ok: true, json: async () => pacotes } as Response);
      }
      if (url.includes("/treinadores")) {
        return Promise.resolve({ ok: true, json: async () => treinadores } as Response);
      }
      return Promise.resolve({ ok: true, json: async () => ({}) } as Response);
    }) as typeof fetch);

    render(<CadastroAlunoPage />);

    fireEvent.click(screen.getByText("Carregar treinadores"));
    fireEvent.click(await screen.findByText("Carlos"));
    fireEvent.click(await screen.findByText("Plano Mensal"));

    fireEvent.change(screen.getByLabelText(/Nome completo/i), { target: { value: "Maria Silva" } });
    fireEvent.change(screen.getByLabelText(/E-mail/i), { target: { value: "maria@email.com" } });
    fireEvent.change(screen.getByLabelText(/Celular/i), { target: { value: "11987654321" } });
    const senhas = screen.getAllByLabelText(/senha/i);
    fireEvent.change(senhas[0], { target: { value: "Senha123abcd" } });
    fireEvent.change(senhas[1], { target: { value: "Senha123abcd" } });
    fireEvent.click(screen.getByText("Próximo"));
    await screen.findByText("Disponibilidade");
  };

  it("'alterar' é botão (role button) e volta ao passo de treinador", async () => {
    const treinadores = [{ treinadorId: "t-1", nome: "Carlos" }];
    const pacotes = [
      { pacoteId: "p-1", nome: "Plano Mensal", descricao: "Acompanhamento", preco: 150, treinadorId: "t-1" },
    ];
    vi.spyOn(global, "fetch").mockImplementation(((url: string) => {
      if (url.includes("/pacotes")) {
        return Promise.resolve({ ok: true, json: async () => pacotes } as Response);
      }
      if (url.includes("/treinadores")) {
        return Promise.resolve({ ok: true, json: async () => treinadores } as Response);
      }
      return Promise.resolve({ ok: true, json: async () => ({}) } as Response);
    }) as typeof fetch);

    render(<CadastroAlunoPage />);
    fireEvent.click(screen.getByText("Carregar treinadores"));
    fireEvent.click(await screen.findByText("Carlos"));
    const alterar = await screen.findByRole("button", { name: /alterar/i });
    expect(alterar).toBeInTheDocument();

    fireEvent.click(alterar);
    expect(screen.queryByText("Plano Mensal")).not.toBeInTheDocument();
    expect(screen.getByText("Carlos")).toBeInTheDocument();
  });

  it("exibe resumo de contratação (valor + cancelamento) antes do CTA final", async () => {
    await fillUntilAnamnese();
    expect(await screen.findByText(/R\$\s?150,00/)).toBeInTheDocument();
    expect(screen.getByText(/7 dias/i)).toBeInTheDocument();
  });

  it("exibe checkbox de consentimento de dados de saúde", async () => {
    await fillUntilAnamnese();
    expect(screen.getByLabelText(/dados de saúde/i)).toBeInTheDocument();
  });

  it("bloqueia o submit enquanto o consentimento não é marcado", async () => {
    await fillUntilAnamnese();
    expect(screen.getByRole("button", { name: /Criar conta/i })).toBeDisabled();
  });

  it("habilita o submit ao marcar o consentimento", async () => {
    await fillUntilAnamnese();
    fireEvent.click(screen.getByLabelText(/dados de saúde/i));
    expect(screen.getByRole("button", { name: /Criar conta/i })).toBeEnabled();
  });

  const selectMui = async (comboIndex: number, optionRe: RegExp) => {
    const combo = screen.getAllByRole("combobox")[comboIndex];
    fireEvent.mouseDown(combo);
    const option = await screen.findByRole("option", { name: optionRe });
    fireEvent.click(option);
  };

  it("envia consentimentoDadosSaude=true e timestamp ISO no payload", async () => {
    await fillUntilAnamnese();

    await selectMui(0, /3 dias/i);
    await selectMui(1, /1 hora/i);
    await selectMui(2, /^Hipertrofia$/i);
    await selectMui(3, /Iniciante/i);

    fireEvent.click(screen.getByLabelText(/dados de saúde/i));
    fireEvent.click(screen.getByRole("button", { name: /Criar conta/i }));

    await waitFor(() => {
      const registerCall = vi
        .mocked(global.fetch)
        .mock.calls.find((c) => String(c[0]).includes("/register/aluno"));
      expect(registerCall).toBeDefined();
      const body = JSON.parse((registerCall![1] as RequestInit).body as string);
      expect(body.consentimentoDadosSaude).toBe(true);
      expect(typeof body.consentimentoDadosSaudeEm).toBe("string");
      expect(() => new Date(body.consentimentoDadosSaudeEm).toISOString()).not.toThrow();
    });
  });
});
