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

// Enums de perfil do aluno
export type FinalidadeTreino =
  | "Hipertrofia"
  | "Emagrecimento"
  | "CondicionamentoFisico"
  | "Saude"
  | "PerformanceEsportiva"
  | "Reabilitacao"
  | "Outro";

export type NivelCondicionamento = "Sedentario" | "Iniciante" | "Intermediario" | "Avancado";

export type TempoDisponivel =
  | "TrintaMinutos"
  | "QuarentaCincoMinutos"
  | "UmaHora"
  | "UmaHoraETrinta"
  | "DuasHoras";

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
  diasDisponiveis: number | null;
  tempoDisponivelMinutos: TempoDisponivel | null;
  finalidade: FinalidadeTreino | null;
  focoTreino: string | null;
  nivelCondicionamento: NivelCondicionamento | null;
  limitacoesFisicas: string | null;
  doencas: string | null;
  observacoesAdicionais: string | null;
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
  temVinculoAtivoPrevio: boolean;
}

// Ficha vinculada ao aluno (visão do treinador)
export interface TreinoAlunoResponse {
  treinoAlunoId: string;
  treinoId: string;
  nomeTreino: string;
  status: TreinoAlunoStatus;
}

// Treino
export interface SerieConfigResponse {
  serieConfigId: string;
  quantidade: number;
  repeticoesMin: number;
  repeticoesMax: number | null;
  descricao: string | null;
  carga: number | null;
  descanso: number | null;
  ordem: number;
}

export interface TreinoExercicioResponse {
  treinoExercicioId: string;
  exercicioId: string;
  nomeExercicio: string;
  series: SerieConfigResponse[];
  ordem: number;
}

export interface TreinoResponse {
  treinoId: string;
  nome: string;
  objetivo: ObjetivoTreino;
  treinadorId: string;
  exercicios: TreinoExercicioResponse[];
  createdAt: string;
  updatedAt: string | null;
  nomeAluno?: string | null;
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

// Grupo Muscular
export interface GrupoMuscularResponse {
  id: string;
  nome: string;
  createdAt: string;
  updatedAt: string | null;
}

// Pacote Aluno
export interface PacoteAlunoResponse {
  pacoteId: string;
  nome: string;
  descricao: string | null;
  preco: number;
  treinadorId: string;
  isAtivo?: boolean;
  createdAt?: string;
  updatedAt?: string | null;
}

// Aluno vinculado a uma ficha
export interface TreinoAlunoVinculado {
  treinoAlunoId: string;
  alunoId: string;
  nomeAluno: string;
  status: string;
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

// Vínculo do aluno (GET /aluno/vinculo)
export interface VinculoAlunoItemResponse {
  vinculoId: string;
  treinadorId: string;
  nomeTreinador: string;
  status: VinculoStatus;
  dataInicio: string | null;
  createdAt: string;
}

export interface MeuVinculoResponse {
  vinculoAtivo: VinculoAlunoItemResponse | null;
  vinculoPendente: VinculoAlunoItemResponse | null;
}

// Paginação
export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  pagina: number;
  tamanhoPagina: number;
}

// Progressão do aluno
export interface PontoProgressao {
  data: string;
  cargaMaxima: number | null;
  seriesExecutadas: number;
  repeticoesExecutadas: number;
}

export interface ExercicioProgressao {
  nomeExercicio: string;
  grupoMuscular: string;
  historico: PontoProgressao[];
}

export interface ProgressaoAlunoResponse {
  exercicios: ExercicioProgressao[];
}

// Erro RFC 7807
export interface ProblemDetails {
  title: string;
  detail?: string;
  status: number;
  errors?: Record<string, string[]>;
}
