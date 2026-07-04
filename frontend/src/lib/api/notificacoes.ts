import { apiClient } from "./client";
import type { NotificacaoResponse } from "@/types";

export interface ContadorNaoLidasResponse {
  total: number;
}

export const notificacoesApi = {
  listar(pagina = 1, tamanhoPagina = 20) {
    return apiClient.get<NotificacaoResponse[]>("/notificacoes", {
      params: { pagina, tamanhoPagina },
    });
  },
  contarNaoLidas() {
    return apiClient.get<ContadorNaoLidasResponse>("/notificacoes/nao-lidas/contador");
  },
  marcarLida(id: string) {
    return apiClient.patch(`/notificacoes/${id}/lida`);
  },
};
