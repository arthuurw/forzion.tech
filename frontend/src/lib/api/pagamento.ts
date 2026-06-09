import { apiClient } from "./client";
import type {
  AssinaturaAlunoResponse,
  AssinaturaTreinadorResponse,
  MetodoPagamento,
  OnboardingStatusResponse,
  PagamentoResponse,
  PagamentoTreinadorStatusResponse,
  TrocarPlanoTreinadorResponse,
} from "@/types";

export const pagamentoApi = {
  iniciarOnboarding(urlRetorno: string, urlCancelamento: string) {
    return apiClient.post<{ url: string }>("/treinador/onboarding", { urlRetorno, urlCancelamento });
  },
  verificarOnboarding() {
    return apiClient.get<OnboardingStatusResponse>("/treinador/onboarding/status");
  },
  alterarModoPagamento(modo: "Plataforma" | "Externo") {
    return apiClient.post<{ modo: string; alteradoEm: string; assinaturasCriadas: number; vinculosIgnorados: number }>(
      "/treinador/modo-pagamento", { modo });
  },
  gerarCobranca(assinaturaId: string, metodo: MetodoPagamento = "Pix") {
    return apiClient.post<PagamentoResponse>(`/treinador/pagamentos/cobrar/${assinaturaId}`, undefined, { params: { metodo } });
  },
  obterPagamento(pagamentoId: string) {
    return apiClient.get<PagamentoResponse>(`/aluno/pagamentos/${pagamentoId}`);
  },
  listarPagamentosAssinatura(assinaturaId: string) {
    return apiClient.get<PagamentoResponse[]>(`/aluno/pagamentos/assinatura/${assinaturaId}`);
  },
  obterMinhaAssinatura() {
    return apiClient.get<AssinaturaAlunoResponse>("/aluno/assinatura");
  },
  cancelarMinhaAssinatura() {
    return apiClient.post<void>("/aluno/assinatura/cancelar");
  },
  obterAssinaturaTreinador() {
    return apiClient.get<AssinaturaTreinadorResponse>("/treinador/plano/assinatura");
  },
  cancelarPlanoTreinador() {
    return apiClient.post<{ canceladaEm: string }>("/treinador/plano/cancelar");
  },
  cobrarRenovacaoPlano(metodo: MetodoPagamento = "Pix") {
    return apiClient.post<{ pagamentoId: string; pixQrCode: string | null; pixQrCodeUrl: string | null; pixExpiracao: string | null; clientSecret: string | null; valor: number; metodoPagamento: MetodoPagamento }>("/treinador/plano/cobrar", undefined, { params: { metodo } });
  },
  trocarPlano(planoPlataformaId: string, metodo: MetodoPagamento = "Pix") {
    return apiClient.post<TrocarPlanoTreinadorResponse>("/treinador/plano/trocar", { planoPlataformaId, metodo });
  },
  obterStatusPagamentoTreinador(pagamentoId: string) {
    return apiClient.get<PagamentoTreinadorStatusResponse>(`/treinador/plano/pagamento/${pagamentoId}`);
  },
  listarPlanosPlataforma() {
    return apiClient.get<import("@/types").PlanoPlataformaResponse[]>("/auth/planos");
  },
};
