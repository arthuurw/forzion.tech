import type { PlanoPlataformaResponse, TierPlano, TreinadorDashboardPlano } from "@/types";

export const TIER_ORDER: TierPlano[] = ["Free", "Basic", "Pro", "ProPlus", "Elite"];

// Única fonte de copy de tier — consumida tanto pelo catálogo admin quanto pelos badges do treinador.
export const TIER_LABEL: Record<TierPlano, string> = {
  Free: "Free",
  Basic: "Basic",
  Pro: "Pro",
  ProPlus: "Pro Plus",
  Elite: "Elite",
};

export function tierAtLeast(tier: TierPlano, minimo: TierPlano): boolean {
  return TIER_ORDER.indexOf(tier) >= TIER_ORDER.indexOf(minimo);
}

export function permiteEmailEngajamento(tier: TierPlano): boolean {
  return tierAtLeast(tier, "Pro");
}

export function permiteWhatsapp(tier: TierPlano): boolean {
  return tierAtLeast(tier, "ProPlus");
}

export type TierEfetivoStatus = "loading" | "error" | "resolved";

export interface TierEfetivoInfo {
  status: TierEfetivoStatus;
  tierEfetivo: TierPlano;
  tierContratado: TierPlano | null;
  divergente: boolean;
}

export function resolverTierEfetivoInfo(
  plano: TreinadorDashboardPlano,
  planos: PlanoPlataformaResponse[],
  planosStatus: TierEfetivoStatus,
): TierEfetivoInfo {
  if (planosStatus !== "resolved") {
    return { status: planosStatus, tierEfetivo: plano.tierEfetivo, tierContratado: null, divergente: false };
  }
  const contratado = planos.find((p) => p.planoId === plano.planoContratadoId) ?? null;
  const tierContratado = contratado?.tier ?? null;
  return {
    status: "resolved",
    tierEfetivo: plano.tierEfetivo,
    tierContratado,
    divergente: tierContratado !== null && tierContratado !== plano.tierEfetivo,
  };
}
