"use client";
import { Chip } from "@mui/material";
import type { PlanoPlataformaResponse, TreinadorDashboardPlano } from "@/types";
import { TIER_LABEL, resolverTierEfetivoInfo } from "@/lib/utils/tier";

interface TierEfetivoBadgeProps {
  plano: TreinadorDashboardPlano;
  planos: PlanoPlataformaResponse[];
}

export default function TierEfetivoBadge({ plano, planos }: TierEfetivoBadgeProps) {
  const info = resolverTierEfetivoInfo(plano, planos);

  if (!info.divergente || info.tierContratado === null) {
    return <Chip label={TIER_LABEL[info.tierEfetivo]} size="small" />;
  }

  return (
    <Chip
      label={`${TIER_LABEL[info.tierEfetivo]} — ${TIER_LABEL[info.tierContratado]} pendente de pagamento`}
      size="small"
      color="warning"
    />
  );
}
