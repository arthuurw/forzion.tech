import { apiClient } from "./client";

export interface EnderecoPublicoPayload {
  rua: string;
  numero?: string | null;
  complemento?: string | null;
  bairro: string;
  cidade: string;
  estado: string;
  cep: string;
}

export interface HorarioFuncionamentoResponse {
  id: string;
  diaSemana: number;
  abreAs: string;
  fechaAs: string;
}

export interface HorarioFuncionamentoPayload {
  diaSemana: number;
  abreAs: string;
  fechaAs: string;
}

export interface PerfilPublicoResponse {
  nomeFantasia: string | null;
  endereco: EnderecoPublicoPayload | null;
  horariosFuncionamento: HorarioFuncionamentoResponse[];
  politicas: Record<string, string> | null;
  isPublicado: boolean;
}

export interface PerfilPublicoPayload {
  nomeFantasia: string | null;
  endereco: EnderecoPublicoPayload | null;
  politicas: Record<string, string> | null;
  horarios: HorarioFuncionamentoPayload[];
  isPublicado: boolean;
}

export const perfilPublicoApi = {
  getPerfilPublico() {
    return apiClient.get<PerfilPublicoResponse | null>("/treinador/perfil-publico");
  },
  salvarPerfilPublico(payload: PerfilPublicoPayload) {
    return apiClient.put<PerfilPublicoResponse>("/treinador/perfil-publico", payload);
  },
};
