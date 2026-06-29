import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, fireEvent, waitFor, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  useParams: () => ({}),
}));

vi.mock("next/dynamic", () => ({
  default: () => {
    const Component = () => null;
    Component.displayName = "DynamicComponent";
    return Component;
  },
}));

import PerfilPage from "../page";
import { renderWithProviders } from "@/test/render";

function meHandler(tipo: "Aluno" | "Treinador") {
  return http.get("*/api/auth/me", () =>
    HttpResponse.json({ contaId: "c1", tipoConta: tipo, perfilId: "p1" }),
  );
}

beforeEach(() => {
  server.use(
    http.get("*/conta/perfil", () =>
      HttpResponse.json({ nome: "João Teste", email: "joao@teste.com", tipoConta: "Aluno" }),
    ),
    http.get("*/aluno/vinculo", () =>
      HttpResponse.json({
        vinculoAtivo: { treinadorId: "t-atual", nomeTreinador: "Treinador Atual", dataInicio: "2026-01-01" },
        vinculoPendente: null,
      }),
    ),
    meHandler("Aluno"),
  );
});

describe("/perfil — dados da conta", () => {
  it("carrega perfil e exibe nome/email", async () => {
    renderWithProviders(<PerfilPage />);
    expect(await screen.findByText("joao@teste.com")).toBeInTheDocument();
    expect(screen.getAllByText("João Teste").length).toBeGreaterThan(0);
  });

  it("erro ao carregar perfil exibe banner", async () => {
    server.use(http.get("*/conta/perfil", () => new HttpResponse(null, { status: 500 })));
    renderWithProviders(<PerfilPage />);
    expect(await screen.findByText(/erro ao carregar perfil/i)).toBeInTheDocument();
  });

  it("salvar perfil com novo nome chama PATCH e mostra sucesso", async () => {
    let patched: unknown = null;
    server.use(
      http.patch("*/conta/perfil", async ({ request }) => {
        patched = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<PerfilPage />);
    const nomeInput = await screen.findByLabelText("Nome");
    fireEvent.change(nomeInput, { target: { value: "João Novo" } });
    fireEvent.click(screen.getByRole("button", { name: /salvar alterações/i }));
    await waitFor(() => expect(patched).toEqual({ nome: "João Novo" }));
    expect(await screen.findByText(/perfil atualizado com sucesso/i)).toBeInTheDocument();
  });

  it("salvar perfil com erro exibe banner", async () => {
    server.use(http.patch("*/conta/perfil", () => new HttpResponse(null, { status: 500 })));
    renderWithProviders(<PerfilPage />);
    const nomeInput = await screen.findByLabelText("Nome");
    fireEvent.change(nomeInput, { target: { value: "Falha" } });
    fireEvent.click(screen.getByRole("button", { name: /salvar alterações/i }));
    expect(await screen.findByText(/erro ao atualizar perfil/i)).toBeInTheDocument();
  });
});

describe("/perfil — alterar senha", () => {
  async function fillSenha(atual: string, nova: string, confirma: string) {
    fireEvent.change(await screen.findByLabelText("Senha atual"), { target: { value: atual } });
    fireEvent.change(screen.getByLabelText("Nova senha"), { target: { value: nova } });
    fireEvent.change(screen.getByLabelText("Confirmar nova senha"), { target: { value: confirma } });
    fireEvent.click(screen.getByRole("button", { name: /alterar senha/i }));
  }

  it("submit vazio exibe erro de campo obrigatório", async () => {
    renderWithProviders(<PerfilPage />);
    fireEvent.click(await screen.findByRole("button", { name: /alterar senha/i }));
    expect(await screen.findByText(/informe a senha atual/i)).toBeInTheDocument();
  });

  it("senhas divergentes mostram erro", async () => {
    renderWithProviders(<PerfilPage />);
    await fillSenha("old12345", "novaSenha1", "outraSenha2");
    expect(await screen.findByText(/as senhas não coincidem/i)).toBeInTheDocument();
  });

  it("senha curta mostra erro de tamanho", async () => {
    renderWithProviders(<PerfilPage />);
    await fillSenha("old12345", "curta", "curta");
    expect(await screen.findByText(/pelo menos 8 caracteres/i)).toBeInTheDocument();
  });

  it("senha válida chama POST e mostra sucesso", async () => {
    let body: unknown = null;
    server.use(
      http.post("*/conta/senha", async ({ request }) => {
        body = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<PerfilPage />);
    await fillSenha("old12345", "novaSenha1", "novaSenha1");
    await waitFor(() => expect(body).toEqual({ senhaAtual: "old12345", novaSenha: "novaSenha1" }));
    expect(await screen.findByText(/senha alterada com sucesso/i)).toBeInTheDocument();
  });

  it("erro no POST de senha exibe banner", async () => {
    server.use(http.post("*/conta/senha", () => new HttpResponse(null, { status: 400 })));
    renderWithProviders(<PerfilPage />);
    await fillSenha("old12345", "novaSenha1", "novaSenha1");
    expect(await screen.findByText(/verifique a senha atual/i)).toBeInTheDocument();
  });
});

describe("/perfil — troca de treinador", () => {
  it("abre dialog, seleciona treinador+pacote e solicita troca", async () => {
    let trocaBody: unknown = null;
    server.use(
      http.get("*/auth/treinadores", () =>
        HttpResponse.json([
          { treinadorId: "t-novo", nome: "Treinador Novo" },
          { treinadorId: "t-atual", nome: "Treinador Atual" },
        ]),
      ),
      http.get("*/auth/treinadores/t-novo/pacotes", () =>
        HttpResponse.json([{ pacoteId: "pac1", nome: "Mensal", descricao: "Plano mensal" }]),
      ),
      http.post("*/aluno/troca-treinador", async ({ request }) => {
        trocaBody = await request.json();
        return HttpResponse.json({ vinculoId: "v1" });
      }),
    );

    renderWithProviders(<PerfilPage />);

    fireEvent.click(await screen.findByRole("button", { name: /solicitar troca/i }));
    const dialog = await screen.findByRole("dialog");

    const treinadorInput = within(dialog).getByLabelText(/novo treinador/i);
    fireEvent.mouseDown(treinadorInput);
    fireEvent.change(treinadorInput, { target: { value: "Novo" } });
    fireEvent.click(await screen.findByText("Treinador Novo"));

    await waitFor(() => expect(within(dialog).getByLabelText(/pacote/i)).toBeEnabled());
    const pacoteInput = within(dialog).getByLabelText(/pacote/i);
    fireEvent.mouseDown(pacoteInput);
    fireEvent.click(await screen.findByText(/Mensal/i));

    fireEvent.click(within(dialog).getByRole("button", { name: /^solicitar$/i }));
    await waitFor(() =>
      expect(trocaBody).toEqual({ novoTreinadorId: "t-novo", pacoteId: "pac1" }),
    );
  });

  it("erro ao carregar treinadores exibe banner", async () => {
    server.use(http.get("*/auth/treinadores", () => new HttpResponse(null, { status: 500 })));
    renderWithProviders(<PerfilPage />);
    fireEvent.click(await screen.findByRole("button", { name: /solicitar troca/i }));
    expect(await screen.findByText(/erro ao carregar treinadores/i)).toBeInTheDocument();
  });
});

describe("/perfil — conta de treinador", () => {
  it("não busca vínculo para Treinador (sem card de treinador)", async () => {
    server.use(
      http.get("*/conta/perfil", () =>
        HttpResponse.json({ nome: "Coach", email: "coach@x.com", tipoConta: "Treinador" }),
      ),
      meHandler("Treinador"),
    );
    renderWithProviders(<PerfilPage />);
    expect(await screen.findByText("coach@x.com")).toBeInTheDocument();
    expect(screen.queryByText("Meu Treinador")).not.toBeInTheDocument();
  });
});
