import { apiClient } from "./client";
import type {
  AlunoResponse,
  VinculoDetalheResponse,
  VinculoStatus,
  TreinoResponse,
  TreinoAlunoResponse,
  TreinoAlunoVinculado,
  ExercicioResponse,
  PacoteAlunoResponse,
  GrupoMuscularResponse,
  PaginatedResponse,
  AlunoStatus,
  ObjetivoTreino,
  ProgressaoAlunoResponse,
} from "@/types";

export interface CriarFichaData {
  alunoId: string;
  nome: string;
  objetivo: ObjetivoTreino;
}

export interface SerieConfigData {
  quantidade: number;
  repeticoesMin: number;
  repeticoesMax?: number | null;
  descricao?: string | null;
  carga?: number | null;
  descanso?: number | null;
}

export interface AdicionarExercicioData {
  exercicioId: string;
  series: SerieConfigData[];
}

export interface CriarExercicioData {
  nome: string;
  descricao?: string | null;
  grupoMuscular?: string | null;
}

export interface CriarPacoteData {
  nome: string;
  preco: number;
  descricao?: string | null;
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
  getProgressaoAluno(alunoId: string, params?: { de?: string; ate?: string }) {
    return apiClient.get<ProgressaoAlunoResponse>(`/treinador/alunos/${alunoId}/progressao`, { params });
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
  listFichas(params?: { pagina?: number; tamanhoPagina?: number; nome?: string; objetivo?: string; ordenarPor?: string }) {
    return apiClient.get<PaginatedResponse<TreinoResponse>>("/treinador/treinos", { params });
  },
  getFicha(treinoId: string) {
    return apiClient.get<TreinoResponse>(`/treinos/${treinoId}`);
  },
  criarFicha(data: CriarFichaData) {
    return apiClient.post<TreinoResponse>("/treinos", data);
  },
  atualizarFicha(treinoId: string, data: { nome?: string; objetivo?: ObjetivoTreino }) {
    return apiClient.patch<TreinoResponse>(`/treinos/${treinoId}`, data);
  },
  excluirFicha(treinoId: string) {
    return apiClient.delete(`/treinos/${treinoId}`);
  },
  duplicarFicha(treinoId: string) {
    return apiClient.post<TreinoResponse>(`/treinos/${treinoId}/duplicar`);
  },
  vincularFichaAoAluno(alunoId: string, treinoId: string) {
    return apiClient.post(`/treinador/alunos/${alunoId}/fichas/${treinoId}`);
  },
  listAlunosVinculados(treinoId: string) {
    return apiClient.get<TreinoAlunoVinculado[]>(`/treinos/${treinoId}/alunos`);
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
    return apiClient.get<GrupoMuscularResponse[]>("/treinador/grupos-musculares");
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
  atualizarPacote(pacoteId: string, data: { nome?: string; preco?: number; descricao?: string | null }) {
    return apiClient.patch<PacoteAlunoResponse>(`/treinador/pacotes/${pacoteId}`, data);
  },
};
