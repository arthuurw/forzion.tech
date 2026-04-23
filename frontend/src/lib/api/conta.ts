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
};
