import { apiClient } from "./client";

export interface PerfilResponse {
  nome: string;
  email: string;
  tipoConta: string;
}

export interface AtualizarPerfilData {
  nome: string;
}

export interface AlterarSenhaData {
  senhaAtual: string;
  novaSenha: string;
}

export const contaApi = {
  getPerfil() {
    return apiClient.get<PerfilResponse>("/conta/perfil");
  },
  atualizarPerfil(data: AtualizarPerfilData) {
    return apiClient.patch("/conta/perfil", data);
  },
  alterarSenha(data: AlterarSenhaData) {
    return apiClient.post("/conta/senha", data);
  },
  exportarDados(formato: "xlsx" | "json" = "xlsx") {
    return apiClient.get("/conta/lgpd/exportar", { params: { formato }, responseType: "blob" });
  },
  /**
   * LGPD — direito ao esquecimento.
   * Exige confirmação de senha. Conta é anonimizada/excluída no backend.
   */
  excluirConta(senha: string) {
    return apiClient.delete("/conta/lgpd", { data: { senha } });
  },
};
