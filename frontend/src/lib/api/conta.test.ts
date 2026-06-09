import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("./client", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), patch: vi.fn(), delete: vi.fn() },
}));

import { apiClient } from "./client";
import { contaApi } from "./conta";

const mock = vi.mocked(apiClient);

beforeEach(() => vi.clearAllMocks());

describe("contaApi", () => {
  it("getPerfil GET", () => {
    contaApi.getPerfil();
    expect(mock.get).toHaveBeenCalledWith("/conta/perfil");
  });

  it("atualizarPerfil PATCH", () => {
    contaApi.atualizarPerfil({ nome: "Novo Nome" });
    expect(mock.patch).toHaveBeenCalledWith("/conta/perfil", { nome: "Novo Nome" });
  });

  it("alterarSenha POST", () => {
    const data = { senhaAtual: "old", novaSenha: "new" };
    contaApi.alterarSenha(data);
    expect(mock.post).toHaveBeenCalledWith("/conta/senha", data);
  });

  it("exportarDados GET xlsx (default)", () => {
    contaApi.exportarDados();
    expect(mock.get).toHaveBeenCalledWith("/conta/lgpd/exportar", { params: { formato: "xlsx" }, responseType: "blob" });
  });

  it("exportarDados GET json", () => {
    contaApi.exportarDados("json");
    expect(mock.get).toHaveBeenCalledWith("/conta/lgpd/exportar", { params: { formato: "json" }, responseType: "blob" });
  });

  it("excluirConta DELETE com senha no body", () => {
    contaApi.excluirConta("minhasenha");
    expect(mock.delete).toHaveBeenCalledWith("/conta/lgpd", { data: { senha: "minhasenha" } });
  });
});
