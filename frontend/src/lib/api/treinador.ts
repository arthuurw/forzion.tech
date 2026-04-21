import { apiClient } from "./client";
import type {
  AlunoResponse,
  VinculoDetalheResponse,
  VinculoStatus,
  TreinoResponse,
  TreinoAlunoResponse,
  ExercicioResponse,
  PacoteAlunoResponse,
  GrupoMuscularResponse,
  PaginatedResponse,
  AlunoStatus,
  ObjetivoTreino,
} from "@/types";

export interface CriarFichaData {
  alunoId: string;
  nome: string;
  objetivo: ObjetivoTreino;
}

export interface AdicionarExercicioData {
  exercicioId: string;
  series: number;
  repeticoes: number;
  carga?: number | null;
  descanso?: number | null;
}

export interface CriarExercicioData {
  nome: string;
  descricao?: string | null;
  grupoMuscular?: string | null;
}

export interface CriarPacoteData {
  nome: string;
  maxFichas: number;
  preco: number;
}

export const treinadorApi = {
  // ── Alunos ──
  listAlunos(params?: { status?: AlunoStatus; pagina?: number; tamanhoPagina?: number }) {
    return apiClient.get<PaginatedResponse<AlunoResponse>>("/treinador/alunos", { params });
  },
  getAluno(alunoId: string) {
    return apiClient.get<AlunoResponse>(`/treinador/alunos/${alunoId}`);
  },
  getFichasDoAluno(alunoId: string) {
    return apiClient.get<TreinoAlunoResponse[]>(`/treinador/alunos/${alunoId}/fichas`);
  },

  // ── Vínculos ──
  listVinculos(params?: { status?: VinculoStatus; pagina?: number; tamanhoPagina?: number }) {
    return apiClient.get<PaginatedResponse<VinculoDetalheResponse>>("/treinador/vinculos", { params });
  },
  aprovarVinculo(vinculoId: string, pacoteId: string, trarFichas = false) {
    return apiClient.post(`/treinador/vinculos/${vinculoId}/aprovar`, { pacoteAlunoId: pacoteId, trarFichas });
  },
  desvincularAluno(vinculoId: string, observacao?: string | null) {
    return apiClient.post(`/treinador/vinculos/${vinculoId}/desvincular`, { observacao: observacao ?? null });
  },

  // ── Fichas ──
  listFichas(params?: { pagina?: number; tamanhoPagina?: number }) {
    return apiClient.get<PaginatedResponse<TreinoResponse>>("/treinador/treinos", { params });
  },
  getFicha(treinoId: string) {
    return apiClient.get<TreinoResponse>(`/treinos/${treinoId}`);
  },
  criarFicha(data: CriarFichaData) {
    return apiClient.post<TreinoResponse>("/treinos", data);
  },
  duplicarFicha(treinoId: string) {
    return apiClient.post<TreinoResponse>(`/treinos/${treinoId}/duplicar`);
  },
  vincularFichaAoAluno(alunoId: string, treinoId: string) {
    return apiClient.post(`/treinador/alunos/${alunoId}/fichas/${treinoId}`);
  },

  // ── Exercícios da ficha ──
  adicionarExercicio(treinoId: string, data: AdicionarExercicioData) {
    return apiClient.post(`/treinos/${treinoId}/exercicios`, data);
  },
  removerExercicio(treinoId: string, exercicioId: string) {
    return apiClient.delete(`/treinos/${treinoId}/exercicios/${exercicioId}`);
  },

  // ── Grupos Musculares ──
  listGruposMusculares() {
    return apiClient.get<GrupoMuscularResponse[]>("/admin/grupos-musculares");
  },

  // ── Biblioteca ──
  listExercicios(params?: { global?: boolean; pagina?: number; tamanhoPagina?: number; nome?: string; grupoMuscular?: string; ordenarPor?: string }) {
    return apiClient.get<PaginatedResponse<ExercicioResponse>>("/treinador/exercicios", { params });
  },
  criarExercicio(data: CriarExercicioData) {
    return apiClient.post<ExercicioResponse>("/treinador/exercicios", data);
  },
  copiarExercicioGlobal(exercicioId: string) {
    return apiClient.post<ExercicioResponse>(`/treinador/exercicios/${exercicioId}/copiar`);
  },
  atualizarExercicio(exercicioId: string, data: { nome?: string; grupoMuscular?: string; descricao?: string | null }) {
    return apiClient.patch<ExercicioResponse>(`/treinador/exercicios/${exercicioId}`, data);
  },
  excluirExercicio(exercicioId: string) {
    return apiClient.delete(`/treinador/exercicios/${exercicioId}`);
  },

  reativarAluno(alunoId: string, pacoteAlunoId: string) {
    return apiClient.post<VinculoDetalheResponse>(`/treinador/alunos/${alunoId}/reativar`, { pacoteAlunoId });
  },

  // ── Pacotes ──
  listPacotes() {
    return apiClient.get<PacoteAlunoResponse[]>("/treinador/pacotes");
  },
  criarPacote(data: CriarPacoteData) {
    return apiClient.post<PacoteAlunoResponse>("/treinador/pacotes", data);
  },
};
