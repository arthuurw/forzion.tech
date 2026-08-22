import { describe, it, expect } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { LeadAdminItem } from "@/types";
import AdminLeadsPage from "../page";

function leadAdmin(overrides: Partial<LeadAdminItem> = {}): LeadAdminItem {
  return {
    id: "lead-1",
    treinadorId: "treinador-1",
    nome: "Maria Silva",
    contatoTipo: "Email",
    contatoValor: "maria@example.com",
    status: "Novo",
    anonimizado: false,
    createdAt: "2026-08-20T10:00:00Z",
    ...overrides,
  };
}

async function buscar(contato = "maria@example.com") {
  render(<AdminLeadsPage />);
  fireEvent.change(screen.getByLabelText(/contato \(e-mail ou telefone\)/i), { target: { value: contato } });
  fireEvent.keyDown(screen.getByLabelText(/contato \(e-mail ou telefone\)/i), { key: "Enter" });
}

describe("AdminLeadsPage", () => {
  it("antes de qualquer busca não mostra resultado nem estado vazio", () => {
    render(<AdminLeadsPage />);
    expect(screen.queryByText(/nenhum lead encontrado/i)).not.toBeInTheDocument();
  });

  it("busca com resultado exibe treinador dono, status e data", async () => {
    server.use(http.get("*/admin/leads", () => HttpResponse.json([leadAdmin()])));
    await buscar();

    expect(await screen.findByText("Maria Silva")).toBeInTheDocument();
    expect(screen.getByText("treinador-1")).toBeInTheDocument();
    expect(screen.getByText("Novo")).toBeInTheDocument();
  });

  it("busca sem resultado exibe mensagem neutra", async () => {
    server.use(http.get("*/admin/leads", () => HttpResponse.json([])));
    await buscar();

    expect(await screen.findByText(/nenhum lead encontrado para esse contato/i)).toBeInTheDocument();
  });

  it("anonimizar confirmado chama o endpoint e exibe sucesso", async () => {
    server.use(http.get("*/admin/leads", () => HttpResponse.json([leadAdmin()])));
    let chamou = false;
    server.use(http.post("*/admin/leads/lead-1/anonimizar", () => {
      chamou = true;
      return new HttpResponse(null, { status: 204 });
    }));
    await buscar();

    fireEvent.click(await screen.findByRole("button", { name: "Anonimizar" }));
    await screen.findByRole("dialog");
    fireEvent.click(screen.getAllByRole("button", { name: "Anonimizar" }).at(-1)!);

    await waitFor(() => expect(chamou).toBe(true));
    expect(await screen.findByText(/anonimizado/i)).toBeInTheDocument();
  });

  it("anonimizar cancelado não chama o endpoint", async () => {
    server.use(http.get("*/admin/leads", () => HttpResponse.json([leadAdmin()])));
    let chamou = false;
    server.use(http.post("*/admin/leads/lead-1/anonimizar", () => {
      chamou = true;
      return new HttpResponse(null, { status: 204 });
    }));
    await buscar();

    fireEvent.click(await screen.findByRole("button", { name: "Anonimizar" }));
    await screen.findByRole("dialog");
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(chamou).toBe(false);
  });

  it("lead já anonimizado exibe botão desabilitado", async () => {
    server.use(http.get("*/admin/leads", () => HttpResponse.json([leadAdmin({ anonimizado: true })])));
    await buscar();

    expect(await screen.findByRole("button", { name: "Já anonimizado" })).toBeDisabled();
  });

  it("erro na busca exibe alerta", async () => {
    server.use(http.get("*/admin/leads", () => HttpResponse.json({ detail: "Erro interno." }, { status: 500 })));
    await buscar();

    expect(await screen.findByText("Erro interno.")).toBeInTheDocument();
  });
});
