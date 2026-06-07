/**
 * Cobre o fluxo de aluno cancelando a própria assinatura via portal:
 *   GET /aluno/assinatura                  -> obterMinhaAssinatura
 *   GET /aluno/pagamentos/assinatura/:id   -> listarPagamentosAssinatura
 *   POST /aluno/assinatura/cancelar        -> cancelarMinhaAssinatura
 *
 * MSW intercepta; apiClient real envia requests.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { contaApi } from "@/lib/api/conta";
import AssinaturaAlunoPage, { CANCELAR_ASSINATURA_DESCRICAO } from "../page";
import type { AssinaturaAlunoResponse } from "@/types";

vi.mock("next/link", () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  ),
}));

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() })),
}));

function makeAssinatura(overrides: Partial<AssinaturaAlunoResponse> = {}): AssinaturaAlunoResponse {
  return {
    assinaturaAlunoId: "ass-1",
    vinculoId: "v-1",
    pacoteId: "p-1",
    treinadorId: "t-1",
    alunoId: "a-1",
    valor: 150,
    status: "Ativa",
    dataInicio: "2026-01-01T00:00:00Z",
    dataProximaCobranca: "2026-06-01T00:00:00Z",
    dataCancelamento: null,
    createdAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

function respondAssinatura(assinatura: AssinaturaAlunoResponse) {
  server.use(
    http.get("*/aluno/assinatura", () => HttpResponse.json(assinatura)),
    http.get("*/aluno/pagamentos/assinatura/:id", () => HttpResponse.json([])),
  );
}

describe("AssinaturaAlunoPage — 204 / sem assinatura", () => {
  afterEach(() => vi.clearAllMocks());

  // Bug 1 — 204 No Content: aluno sem assinatura → empty state, sem crash
  it("204 No Content → exibe estado vazio sem crash", async () => {
    server.use(
      http.get("*/aluno/assinatura", () => new HttpResponse(null, { status: 204 })),
    );

    render(<AssinaturaAlunoPage />);
    expect(await screen.findByText("Você não possui assinatura ativa no momento.")).toBeInTheDocument();
    expect(screen.queryByText("Cancelar assinatura")).not.toBeInTheDocument();
  });

  it("200 com payload sem assinaturaAlunoId → empty state, sem crash", async () => {
    server.use(
      http.get("*/aluno/assinatura", () => HttpResponse.json({})),
    );

    render(<AssinaturaAlunoPage />);
    expect(await screen.findByText("Você não possui assinatura ativa no momento.")).toBeInTheDocument();
  });
});

describe("AssinaturaAlunoPage — cancelar assinatura", () => {
  afterEach(() => vi.clearAllMocks());

  it("Status Ativa → exibe botão 'Cancelar assinatura'", async () => {
    respondAssinatura(makeAssinatura({ status: "Ativa" }));

    render(<AssinaturaAlunoPage />);

    expect(await screen.findByRole("button", { name: "Cancelar assinatura" })).toBeInTheDocument();
  });

  it("Status Inadimplente → exibe botão 'Cancelar assinatura'", async () => {
    respondAssinatura(makeAssinatura({ status: "Inadimplente" }));

    render(<AssinaturaAlunoPage />);

    expect(await screen.findByRole("button", { name: "Cancelar assinatura" })).toBeInTheDocument();
  });

  it("Status Pendente → não exibe botão 'Cancelar assinatura'", async () => {
    respondAssinatura(makeAssinatura({ status: "Pendente" }));

    render(<AssinaturaAlunoPage />);

    expect(await screen.findByText("Pendente")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancelar assinatura" })).not.toBeInTheDocument();
  });

  it("Status Cancelada → não exibe botão 'Cancelar assinatura'", async () => {
    respondAssinatura(makeAssinatura({ status: "Cancelada", dataCancelamento: "2026-05-01T00:00:00Z" }));

    render(<AssinaturaAlunoPage />);

    expect(await screen.findByText("Cancelada")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancelar assinatura" })).not.toBeInTheDocument();
  });

  it("Clicar em 'Cancelar assinatura' abre o ConfirmDialog com descrição esperada", async () => {
    respondAssinatura(makeAssinatura({ status: "Ativa" }));

    render(<AssinaturaAlunoPage />);

    const triggerBtn = await screen.findByRole("button", { name: "Cancelar assinatura" });
    fireEvent.click(triggerBtn);

    expect(await screen.findByText(CANCELAR_ASSINATURA_DESCRICAO)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Voltar" })).toBeInTheDocument();
  });

  it("Confirmar no dialog chama POST /aluno/assinatura/cancelar e recarrega", async () => {
    let postCalls = 0;
    let assinaturaAtual = makeAssinatura({ status: "Ativa" });
    server.use(
      http.get("*/aluno/assinatura", () => HttpResponse.json(assinaturaAtual)),
      http.get("*/aluno/pagamentos/assinatura/:id", () => HttpResponse.json([])),
      http.post("*/aluno/assinatura/cancelar", () => {
        postCalls++;
        assinaturaAtual = { ...assinaturaAtual, status: "Cancelada", dataCancelamento: "2026-05-29T00:00:00Z" };
        return new HttpResponse(null, { status: 200 });
      }),
    );

    render(<AssinaturaAlunoPage />);

    const triggerBtn = await screen.findByRole("button", { name: "Cancelar assinatura" });
    fireEvent.click(triggerBtn);

    // Botão dentro do dialog com o mesmo label
    const confirmBtns = await screen.findAllByRole("button", { name: "Cancelar assinatura" });
    // O último é o do dialog (o primeiro é o trigger)
    fireEvent.click(confirmBtns[confirmBtns.length - 1]);

    await waitFor(() => expect(postCalls).toBe(1));
    // Após reload, status Cancelada removeu o botão de cancelar
    await waitFor(() => expect(screen.queryByRole("button", { name: "Cancelar assinatura" })).not.toBeInTheDocument());
    expect(await screen.findByText("Assinatura cancelada com sucesso.")).toBeInTheDocument();
  });

  it("Dialog exibe 'Baixar meus dados' e clique chama contaApi.exportarDados (Marco Civil art. 16)", async () => {
    respondAssinatura(makeAssinatura({ status: "Ativa" }));
    const exportSpy = vi
      .spyOn(contaApi, "exportarDados")
      .mockResolvedValue({ data: new Blob(["{}"], { type: "application/json" }) } as Awaited<ReturnType<typeof contaApi.exportarDados>>);
    const createObjUrl = vi.spyOn(URL, "createObjectURL").mockReturnValue("blob:fake");
    const revokeObjUrl = vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => {});

    render(<AssinaturaAlunoPage />);

    fireEvent.click(await screen.findByRole("button", { name: "Cancelar assinatura" }));

    const baixar = await screen.findByRole("button", { name: /baixar meus dados/i });
    fireEvent.click(baixar);

    await waitFor(() => expect(exportSpy).toHaveBeenCalledTimes(1));

    createObjUrl.mockRestore();
    revokeObjUrl.mockRestore();
    exportSpy.mockRestore();
  });

  it("API retornar erro com detail → exibe o detail do backend e mantém botão", async () => {
    server.use(
      http.get("*/aluno/assinatura", () => HttpResponse.json(makeAssinatura({ status: "Ativa" }))),
      http.get("*/aluno/pagamentos/assinatura/:id", () => HttpResponse.json([])),
      http.post("*/aluno/assinatura/cancelar", () =>
        HttpResponse.json({ detail: "Assinatura já cancelada." }, { status: 409 }),
      ),
    );

    render(<AssinaturaAlunoPage />);

    fireEvent.click(await screen.findByRole("button", { name: "Cancelar assinatura" }));
    const confirmBtns = await screen.findAllByRole("button", { name: "Cancelar assinatura" });
    fireEvent.click(confirmBtns[confirmBtns.length - 1]);

    expect(await screen.findByText("Assinatura já cancelada.")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Cancelar assinatura" }).length).toBeGreaterThan(0);
  });

  it("API retornar erro sem mensagem → exibe fallback genérico", async () => {
    server.use(
      http.get("*/aluno/assinatura", () => HttpResponse.json(makeAssinatura({ status: "Ativa" }))),
      http.get("*/aluno/pagamentos/assinatura/:id", () => HttpResponse.json([])),
      http.post("*/aluno/assinatura/cancelar", () => new HttpResponse(null, { status: 500 })),
    );

    render(<AssinaturaAlunoPage />);

    fireEvent.click(await screen.findByRole("button", { name: "Cancelar assinatura" }));
    const confirmBtns = await screen.findAllByRole("button", { name: "Cancelar assinatura" });
    fireEvent.click(confirmBtns[confirmBtns.length - 1]);

    expect(await screen.findByText("Não foi possível cancelar a assinatura. Tente novamente.")).toBeInTheDocument();
  });
});
