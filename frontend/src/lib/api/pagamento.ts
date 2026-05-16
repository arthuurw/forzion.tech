import { apiClient } from "./client";
import type { AssinaturaResponse, MetodoPagamento, OnboardingStatusResponse, PagamentoResponse } from "@/types";

export const pagamentoApi = {
  // ── Onboarding Treinador ──
  iniciarOnboarding(urlRetorno: string, urlCancelamento: string) {
    return apiClient.post<{ url: string }>("/treinador/onboarding", { urlRetorno, urlCancelamento });
  },
  verificarOnboarding() {
    return apiClient.get<OnboardingStatusResponse>("/treinador/onboarding/status");
  },

  // ── Cobrança (treinador) ──
  gerarCobranca(assinaturaId: string, metodo: MetodoPagamento = "Pix") {
    return apiClient.post<PagamentoResponse>(`/treinador/pagamentos/cobrar/${assinaturaId}?metodo=${metodo}`);
  },

  // ── Pagamentos (aluno) ──
  obterPagamento(pagamentoId: string) {
    return apiClient.get<PagamentoResponse>(`/aluno/pagamentos/${pagamentoId}`);
  },
  listarPagamentosAssinatura(assinaturaId: string) {
    return apiClient.get<PagamentoResponse[]>(`/aluno/pagamentos/assinatura/${assinaturaId}`);
  },

  // ── Assinatura do aluno ──
  obterAssinatura(assinaturaId: string) {
    return apiClient.get<AssinaturaResponse>(`/aluno/assinaturas/${assinaturaId}`);
  },
};
