import type { PlanoPlataformaResponse, TierPlano, TreinadorDashboardPlano } from "@/types";

const TIER_ORDER: TierPlano[] = ["Free", "Basic", "Pro", "ProPlus", "Elite"];

export const TIER_LABEL: Record<TierPlano, string> = {
  Free: "Free",
  Basic: "Basic",
  Pro: "Pro",
  ProPlus: "Pro+",
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

export interface TierEfetivoInfo {
  tierEfetivo: TierPlano;
  tierContratado: TierPlano | null;
  divergente: boolean;
}

export function resolverTierEfetivoInfo(
  plano: TreinadorDashboardPlano,
  planos: PlanoPlataformaResponse[],
): TierEfetivoInfo {
  const contratado = planos.find((p) => p.planoId === plano.planoContratadoId) ?? null;
  const tierContratado = contratado?.tier ?? null;
  return {
    tierEfetivo: plano.tierEfetivo,
    tierContratado,
    divergente: tierContratado !== null && tierContratado !== plano.tierEfetivo,
  };
}
