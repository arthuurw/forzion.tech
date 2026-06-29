import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  useParams: () => ({}),
}));

import AnamnesePage from "../page";
import { renderWithProviders } from "@/test/render";

const anamneseExistente = {
  alunoId: "p1",
  nome: "João",
  email: "joao@teste.com",
  telefone: null,
  status: "Ativo",
  contaId: "c1",
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null,
  diasDisponiveis: 4,
  tempoDisponivelMinutos: 60,
  finalidade: "Hipertrofia",
  focoTreino: "core",
  nivelCondicionamento: "Intermediario",
  limitacoesFisicas: null,
  doencas: "hipertensao",
  observacoesAdicionais: null,
};

beforeEach(() => {
  server.use(
    http.get("*/api/auth/me", () =>
      HttpResponse.json({ contaId: "c1", tipoConta: "Aluno", perfilId: "p1" }),
    ),
    http.get("*/alunos/p1", () => HttpResponse.json(anamneseExistente)),
  );
});

describe("/perfil/anamnese", () => {
  it("prefill: carrega a anamnese atual no formulário", async () => {
    renderWithProviders(<AnamnesePage />);

    const foco = await screen.findByLabelText(/foco de treino/i);
    expect((foco as HTMLInputElement).value).toBe("core");
    expect((screen.getByLabelText(/doenças/i) as HTMLInputElement).value).toBe("hipertensao");
  });

  it("consent gating: salvar bloqueado até consentir o tratamento de dados de saúde", async () => {
    renderWithProviders(<AnamnesePage />);

    await screen.findByLabelText(/foco de treino/i);
    const salvar = screen.getByRole("button", { name: /salvar anamnese/i });
    expect(salvar).toBeDisabled();

    fireEvent.click(screen.getByRole("checkbox"));
    expect(salvar).toBeEnabled();
  });

  it("happy: consentir e salvar dispara PUT /aluno/anamnese com consentimento", async () => {
    let putBody: any = null;
    server.use(
      http.put("*/aluno/anamnese", async ({ request }) => {
        putBody = await request.json();
        return HttpResponse.json({ ...anamneseExistente });
      }),
    );

    renderWithProviders(<AnamnesePage />);

    await screen.findByLabelText(/foco de treino/i);
    fireEvent.click(screen.getByRole("checkbox"));
    fireEvent.click(screen.getByRole("button", { name: /salvar anamnese/i }));

    await waitFor(() => {
      expect(putBody).not.toBeNull();
      expect(putBody.consentimentoDadosSaude).toBe(true);
      expect(putBody.consentimentoDadosSaudeEm).toEqual(expect.any(String));
      expect(putBody.diasDisponiveis).toBe(4);
      expect(putBody.doencas).toBe("hipertensao");
    });

    expect(await screen.findByText(/anamnese atualizada com sucesso/i)).toBeInTheDocument();
  });

  it("edge: aluno sem anamnese prévia renderiza o formulário em branco", async () => {
    server.use(
      http.get("*/alunos/p1", () =>
        HttpResponse.json({
          ...anamneseExistente,
          diasDisponiveis: null,
          tempoDisponivelMinutos: null,
          finalidade: null,
          focoTreino: null,
          nivelCondicionamento: null,
          doencas: null,
        }),
      ),
    );

    renderWithProviders(<AnamnesePage />);

    const foco = await screen.findByLabelText(/foco de treino/i);
    expect((foco as HTMLInputElement).value).toBe("");
    expect(screen.getByRole("button", { name: /salvar anamnese/i })).toBeInTheDocument();
  });
});
