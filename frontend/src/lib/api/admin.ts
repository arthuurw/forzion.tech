import { apiClient } from "./client";
import type {
  TreinadorResponse,
  PlanoTreinadorResponse,
  ExercicioResponse,
  GrupoMuscularResponse,
  PaginatedResponse,
  TreinadorStatus,
} from "@/types";

export type GrupoMuscularEnum =
  | "Peito" | "Costas" | "Ombro" | "Biceps" | "Triceps"
  | "Pernas" | "Gluteos" | "Core" | "FullBody";

export interface ListarExerciciosGlobaisResponse {
  items: ExercicioResponse[];
  total: number;
  pagina: number;
  tamanhoPagina: number;
}

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

  reprovarTreinador(treinadorId: string, observacao?: string | null) {
    return apiClient.post(`/admin/treinadores/${treinadorId}/reprovar`, { observacao: observacao ?? null });
  },

  inativarTreinador(treinadorId: string, observacao?: string | null) {
    return apiClient.post(`/admin/treinadores/${treinadorId}/inativar`, { observacao: observacao ?? null });
  },

  excluirTreinador(treinadorId: string) {
    return apiClient.delete(`/admin/treinadores/${treinadorId}`);
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

  atualizarPlano(planoId: string, data: { nome?: string; maxAlunos?: number; preco?: number }) {
    return apiClient.patch<PlanoTreinadorResponse>(`/admin/planos/${planoId}`, data);
  },

  excluirPlano(planoId: string) {
    return apiClient.delete(`/admin/planos/${planoId}`);
  },

  // Grupos Musculares
  listGruposMusculares() {
    return apiClient.get<GrupoMuscularResponse[]>("/admin/grupos-musculares");
  },

  criarGrupoMuscular(nome: string) {
    return apiClient.post<GrupoMuscularResponse>("/admin/grupos-musculares", { nome });
  },

  atualizarGrupoMuscular(id: string, nome: string) {
    return apiClient.patch<GrupoMuscularResponse>(`/admin/grupos-musculares/${id}`, { nome });
  },

  excluirGrupoMuscular(id: string) {
    return apiClient.delete(`/admin/grupos-musculares/${id}`);
  },

  listExerciciosGlobais(params?: { pagina?: number; tamanhoPagina?: number; nome?: string; grupoMuscular?: string; ordenarPor?: string }) {
    return apiClient.get<ListarExerciciosGlobaisResponse>("/admin/exercicios", { params });
  },

  criarExercicioGlobal(data: { nome: string; grupoMuscular: GrupoMuscularEnum; descricao?: string | null }) {
    return apiClient.post<ExercicioResponse>("/admin/exercicios", data);
  },

  atualizarExercicioGlobal(exercicioId: string, data: { nome?: string; grupoMuscular?: GrupoMuscularEnum; descricao?: string | null }) {
    return apiClient.patch<ExercicioResponse>(`/admin/exercicios/${exercicioId}`, data);
  },

  excluirExercicioGlobal(exercicioId: string) {
    return apiClient.delete(`/admin/exercicios/${exercicioId}`);
  },
};
