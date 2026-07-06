"use client";
import { Chip, Tooltip } from "@mui/material";
import type { PlanoPlataformaResponse, TreinadorDashboardPlano } from "@/types";
import { TIER_LABEL, resolverTierEfetivoInfo, type TierEfetivoStatus } from "@/lib/utils/tier";

interface TierEfetivoBadgeProps {
  plano: TreinadorDashboardPlano;
  planos: PlanoPlataformaResponse[];
  planosStatus: TierEfetivoStatus;
}

export default function TierEfetivoBadge({ plano, planos, planosStatus }: TierEfetivoBadgeProps) {
  const info = resolverTierEfetivoInfo(plano, planos, planosStatus);

  if (info.status !== "resolved") {
    return (
      <Tooltip title="Não foi possível confirmar se o tier contratado corresponde ao ativo.">
        <Chip label={TIER_LABEL[info.tierEfetivo]} size="small" variant="outlined" />
      </Tooltip>
    );
  }

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
