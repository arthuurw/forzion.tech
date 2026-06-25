import { describe, it, expect } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import SemVinculoAtivoBanner from "./SemVinculoAtivoBanner";

const VINCULO = {
  vinculoId: "v-1", treinadorId: "t-1", nomeTreinador: "Carlos",
  status: "Ativo", dataInicio: null, createdAt: "2026-01-01T00:00:00Z",
};

describe("SemVinculoAtivoBanner — modo prop (vinculo do agregado)", () => {
  it("vinculo.ativo=true não renderiza nada", () => {
    render(<SemVinculoAtivoBanner vinculo={{ ativo: true, pendente: false }} />);
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("vinculo={ativo:false,pendente:false} exibe aviso de histórico read-only", () => {
    render(<SemVinculoAtivoBanner vinculo={{ ativo: false, pendente: false }} />);
    expect(screen.getByText(/não tem um vínculo ativo/)).toBeInTheDocument();
    expect(screen.getByText(/registro de novos treinos fica bloqueado/)).toBeInTheDocument();
  });

  it("vinculo={ativo:false,pendente:true} exibe aviso de aguardando aprovação", () => {
    render(<SemVinculoAtivoBanner vinculo={{ ativo: false, pendente: true }} />);
    expect(screen.getByText(/aguardando aprovação do treinador/)).toBeInTheDocument();
  });
});

describe("SemVinculoAtivoBanner — modo sem prop (fetch interno para páginas legadas)", () => {
  function vinculoHandler(body: { vinculoAtivo: unknown; vinculoPendente: unknown }) {
    server.use(http.get("*/aluno/vinculo", () => HttpResponse.json(body)));
  }

  it("com vínculo ativo não renderiza nada", async () => {
    vinculoHandler({ vinculoAtivo: VINCULO, vinculoPendente: null });
    render(<SemVinculoAtivoBanner />);
    await waitFor(() => expect(screen.queryByRole("alert")).not.toBeInTheDocument());
  });

  it("sem vínculo exibe aviso de histórico read-only", async () => {
    vinculoHandler({ vinculoAtivo: null, vinculoPendente: null });
    render(<SemVinculoAtivoBanner />);
    expect(await screen.findByText(/não tem um vínculo ativo/)).toBeInTheDocument();
  });

  it("vínculo pendente exibe aviso de aguardando aprovação", async () => {
    vinculoHandler({ vinculoAtivo: null, vinculoPendente: { ...VINCULO, status: "AguardandoAprovacao" } });
    render(<SemVinculoAtivoBanner />);
    expect(await screen.findByText(/aguardando aprovação do treinador/)).toBeInTheDocument();
  });

  it("erro na consulta não exibe banner (falha segura)", async () => {
    server.use(http.get("*/aluno/vinculo", () => HttpResponse.error()));
    render(<SemVinculoAtivoBanner />);
    await waitFor(() => expect(screen.queryByRole("alert")).not.toBeInTheDocument());
  });
});
