import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("./client", () => ({
  apiClient: { get: vi.fn(), post: vi.fn() },
}));

import { apiClient } from "./client";
import { pagamentoApi } from "./pagamento";

const mock = vi.mocked(apiClient);

beforeEach(() => vi.clearAllMocks());

describe("pagamentoApi", () => {
  it("iniciarOnboarding POST com urls", () => {
    pagamentoApi.iniciarOnboarding("https://ret", "https://cancel");
    expect(mock.post).toHaveBeenCalledWith("/treinador/onboarding", {
      urlRetorno: "https://ret",
      urlCancelamento: "https://cancel",
    });
  });

  it("verificarOnboarding GET", () => {
    pagamentoApi.verificarOnboarding();
    expect(mock.get).toHaveBeenCalledWith("/treinador/onboarding/status");
  });

  it("gerarCobranca POST com metodo default Pix", () => {
    pagamentoApi.gerarCobranca("a1");
    expect(mock.post).toHaveBeenCalledWith("/treinador/pagamentos/cobrar/a1", undefined, { params: { metodo: "Pix" } });
  });

  it("gerarCobranca POST com metodo Cartao", () => {
    pagamentoApi.gerarCobranca("a1", "Cartao");
    expect(mock.post).toHaveBeenCalledWith("/treinador/pagamentos/cobrar/a1", undefined, { params: { metodo: "Cartao" } });
  });

  it("obterPagamento GET", () => {
    pagamentoApi.obterPagamento("pg1");
    expect(mock.get).toHaveBeenCalledWith("/aluno/pagamentos/pg1");
  });

  it("listarPagamentosAssinatura GET com paginação default", () => {
    pagamentoApi.listarPagamentosAssinatura("as1");
    expect(mock.get).toHaveBeenCalledWith("/aluno/pagamentos/assinatura/as1", {
      params: { pagina: 1, tamanhoPagina: 20 },
    });
  });

  it("obterMinhaAssinatura GET", () => {
    pagamentoApi.obterMinhaAssinatura();
    expect(mock.get).toHaveBeenCalledWith("/aluno/assinatura");
  });

  it("cancelarMinhaAssinatura POST", () => {
    pagamentoApi.cancelarMinhaAssinatura();
    expect(mock.post).toHaveBeenCalledWith("/aluno/assinatura/cancelar");
  });

  it("cancelarPlanoTreinador POST sem body", () => {
    pagamentoApi.cancelarPlanoTreinador();
    expect(mock.post).toHaveBeenCalledWith("/treinador/plano/cancelar");
  });
});
