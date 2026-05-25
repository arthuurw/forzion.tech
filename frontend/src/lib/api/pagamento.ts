import { apiClient } from "./client";
import type { AssinaturaAlunoResponse, MetodoPagamento, OnboardingStatusResponse, PagamentoResponse } from "@/types";

export const pagamentoApi = {
  iniciarOnboarding(urlRetorno: string, urlCancelamento: string) {
    return apiClient.post<{ url: string }>("/treinador/onboarding", { urlRetorno, urlCancelamento });
  },
  verificarOnboarding() {
    return apiClient.get<OnboardingStatusResponse>("/treinador/onboarding/status");
  },
  gerarCobranca(assinaturaId: string, metodo: MetodoPagamento = "Pix") {
    return apiClient.post<PagamentoResponse>(`/treinador/pagamentos/cobrar/${assinaturaId}?metodo=${metodo}`);
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
  obterAssinatura(assinaturaId: string) {
    return apiClient.get<AssinaturaAlunoResponse>(`/aluno/assinaturas/${assinaturaId}`);
  },
};
