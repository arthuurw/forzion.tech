import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, fireEvent, waitFor } from "@testing-library/react";
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

beforeEach(() => {
  server.use(
    http.get("*/conta/perfil", () =>
      HttpResponse.json({ nome: "João Teste", email: "joao@teste.com", tipoConta: "Aluno", emailEngajamentoOptOut: false }),
    ),
    http.get("*/aluno/vinculo", () =>
      HttpResponse.json({ vinculoAtivo: null, vinculoPendente: null }),
    ),
    http.get("*/api/auth/me", () =>
      HttpResponse.json({ contaId: "c1", tipoConta: "Aluno", perfilId: "p1" }),
    ),
  );
});

async function findToggle() {
  return screen.findByRole("switch", { name: /receber e-mails de engajamento/i });
}

describe("/perfil — preferência de notificações", () => {
  it("renderiza o toggle de engajamento marcado por padrão", async () => {
    renderWithProviders(<PerfilPage />);
    expect(await findToggle()).toBeChecked();
  });

  it("reflete opt-out persistido: toggle desmarcado quando emailEngajamentoOptOut=true", async () => {
    server.use(
      http.get("*/conta/perfil", () =>
        HttpResponse.json({ nome: "João Teste", email: "joao@teste.com", tipoConta: "Aluno", emailEngajamentoOptOut: true }),
      ),
    );
    renderWithProviders(<PerfilPage />);
    expect(await findToggle()).not.toBeChecked();
  });

  it("desmarcar envia opt-out=true e mostra sucesso", async () => {
    let body: unknown = null;
    server.use(
      http.patch("*/conta/preferencias-notificacao", async ({ request }) => {
        body = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<PerfilPage />);
    fireEvent.click(await findToggle());
    await waitFor(() => expect(body).toEqual({ emailEngajamentoOptOut: true }));
    expect(await screen.findByText(/e-mails de engajamento desativados/i)).toBeInTheDocument();
    expect(await findToggle()).not.toBeChecked();
  });

  it("marcar de volta envia opt-out=false", async () => {
    const bodies: unknown[] = [];
    server.use(
      http.patch("*/conta/preferencias-notificacao", async ({ request }) => {
        bodies.push(await request.json());
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<PerfilPage />);
    const toggle = await findToggle();
    fireEvent.click(toggle);
    await waitFor(() => expect(bodies).toHaveLength(1));
    fireEvent.click(toggle);
    await waitFor(() => expect(bodies).toHaveLength(2));
    expect(bodies[1]).toEqual({ emailEngajamentoOptOut: false });
    expect(await findToggle()).toBeChecked();
  });

  it("erro no PATCH reverte o toggle e exibe banner", async () => {
    server.use(
      http.patch("*/conta/preferencias-notificacao", () => new HttpResponse(null, { status: 500 })),
    );
    renderWithProviders(<PerfilPage />);
    fireEvent.click(await findToggle());
    expect(await screen.findByText(/erro ao salvar preferência de notificações/i)).toBeInTheDocument();
    expect(await findToggle()).toBeChecked();
  });
});
