export type TipoConta = "SystemAdmin" | "Treinador" | "Aluno";

export type AlunoStatus = "AguardandoAprovacao" | "Ativo" | "Inativo";
export type TreinadorStatus = "AguardandoAprovacao" | "Ativo" | "Inativo";
export type VinculoStatus = "AguardandoAprovacao" | "Ativo" | "Inativo";
export type TreinoAlunoStatus = "Ativo" | "Inativo";
export type ObjetivoTreino =
  | "Hipertrofia"
  | "Emagrecimento"
  | "Resistencia"
  | "Forca"
  | "Flexibilidade"
  | "Condicionamento";

// Auth
export interface LoginResponse {
  token: string;
  tipoConta: TipoConta;
  contaId: string;
  perfilId: string;
}

export type SessionUser = LoginResponse;

// Aluno
export interface AlunoResponse {
  alunoId: string;
  nome: string;
  email: string | null;
  telefone: string | null;
  status: AlunoStatus;
  contaId: string;
  createdAt: string;
  updatedAt: string | null;
}

// Treinador
export interface TreinadorResponse {
  treinadorId: string;
  nome: string;
  contaId: string;
  status: TreinadorStatus;
  planoTreinadorId: string | null;
  createdAt: string;
}

// Vínculo
export interface VinculoResponse {
  vinculoId: string;
  treinadorId: string;
  alunoId: string;
  pacoteAlunoId: string | null;
  status: VinculoStatus;
  createdAt: string;
}

export interface VinculoDetalheResponse extends VinculoResponse {
  nomeAluno: string;
  emailAluno: string | null;
}

// Ficha vinculada ao aluno (visão do treinador)
export interface TreinoAlunoResponse {
  treinoAlunoId: string;
  treinoId: string;
  nomeTreino: string;
  status: TreinoAlunoStatus;
}

// Treino
export interface TreinoExercicioResponse {
  treinoExercicioId: string;
  exercicioId: string;
  nomeExercicio: string;
  series: number;
  repeticoes: number;
  carga: number | null;
  descansoSegundos: number | null;
}

export interface TreinoResponse {
  treinoId: string;
  nome: string;
  objetivo: ObjetivoTreino;
  treinadorId: string;
  exercicios: TreinoExercicioResponse[];
  createdAt: string;
  updatedAt: string | null;
}

// Exercício
export interface ExercicioResponse {
  exercicioId: string;
  nome: string;
  descricao: string | null;
  grupoMuscular: string | null;
  treinadorId: string | null;
  isGlobal: boolean;
}

// Plano Treinador
export interface PlanoTreinadorResponse {
  planoId: string;
  nome: string;
  maxAlunos: number;
  preco: number;
  isAtivo?: boolean;
  createdAt?: string;
  updatedAt?: string | null;
}

// Pacote Aluno
export interface PacoteAlunoResponse {
  pacoteId: string;
  nome: string;
  maxFichas: number;
  preco: number;
  treinadorId: string;
  isAtivo?: boolean;
  createdAt?: string;
  updatedAt?: string | null;
}

// Execução
export interface ExecucaoTreinoResponse {
  execucaoId: string;
  treinoId: string;
  alunoId: string;
  dataExecucao: string;
  observacao: string | null;
  createdAt: string;
}

// Paginação
export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  pagina: number;
  tamanhoPagina: number;
}

// Erro RFC 7807
export interface ProblemDetails {
  title: string;
  detail?: string;
  status: number;
  errors?: Record<string, string[]>;
}
