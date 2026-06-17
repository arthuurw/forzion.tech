import { apiClient } from "./client";

export const MfaFator = { Totp: 0, Email: 1, RecoveryCode: 2 } as const;
export type MfaFatorValue = (typeof MfaFator)[keyof typeof MfaFator];

export interface MfaDispositivo {
  id: string;
  rotulo: string | null;
  criadoEm: string;
  ultimoUsoEm: string | null;
  expiraEm: string;
}

export interface MfaStatus {
  habilitado: boolean;
  recoveryCodesRestantes: number;
  dispositivos: MfaDispositivo[];
}

export interface IniciarTotpResult {
  secretBase32: string;
  otpauthUri: string;
}

export interface RecoveryCodesResult {
  recoveryCodes: string[];
}

export interface IniciarStepUpResult {
  fator: MfaFatorValue;
}

export interface StepUpTokenResult {
  token: string;
  expiraEm: string;
}

const stepUpHeader = (token: string) => ({ headers: { "X-Step-Up-Token": token } });

export const mfaApi = {
  getStatus() {
    return apiClient.get<MfaStatus>("/conta/mfa/status");
  },
  iniciarTotp() {
    return apiClient.post<IniciarTotpResult>("/conta/mfa/totp/iniciar");
  },
  confirmarTotp(codigo: string) {
    return apiClient.post<RecoveryCodesResult>("/conta/mfa/totp/confirmar", { codigo });
  },
  desabilitar(stepUpToken: string) {
    return apiClient.post("/conta/mfa/desabilitar", null, stepUpHeader(stepUpToken));
  },
  regenerarRecovery(stepUpToken: string) {
    return apiClient.post<RecoveryCodesResult>("/conta/mfa/recovery/regenerar", null, stepUpHeader(stepUpToken));
  },
  iniciarStepUp() {
    return apiClient.post<IniciarStepUpResult>("/auth/step-up/iniciar");
  },
  verificarStepUp(codigo: string) {
    return apiClient.post<StepUpTokenResult>("/auth/step-up/verificar", { codigo });
  },
};
