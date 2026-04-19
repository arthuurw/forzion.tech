import { apiClient } from "./client";
import type {
  TreinadorResponse,
  PlanoTreinadorResponse,
  PaginatedResponse,
  TreinadorStatus,
} from "@/types";

export interface ListTreinadoresParams {
  status?: TreinadorStatus;
  pagina?: number;
  tamanhoPagina?: number;
}

export const adminApi = {
  listTreinadores(params?: ListTreinadoresParams) {
    return apiClient.get<PaginatedResponse<TreinadorResponse>>("/admin/treinadores", { params });
  },

  aprovarTreinador(treinadorId: string, observacao?: string | null) {
    return apiClient.post(`/admin/treinadores/${treinadorId}/aprovar`, { observacao: observacao ?? null });
  },

  inativarTreinador(treinadorId: string, observacao?: string | null) {
    return apiClient.post(`/admin/treinadores/${treinadorId}/inativar`, { observacao: observacao ?? null });
  },

  atribuirPlano(treinadorId: string, planoId: string) {
    return apiClient.patch(`/admin/treinadores/${treinadorId}/plano`, { planoId });
  },

  listPlanos() {
    return apiClient.get<PlanoTreinadorResponse[]>("/admin/planos");
  },

  criarPlano(nome: string, maxAlunos: number, preco: number) {
    return apiClient.post<PlanoTreinadorResponse>("/admin/planos", { nome, maxAlunos, preco });
  },
};
