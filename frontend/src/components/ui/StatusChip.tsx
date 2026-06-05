"use client";
import { Chip } from "@mui/material";
import type { AlunoStatus, TreinadorStatus, VinculoStatus } from "@/types";

type Status = AlunoStatus | TreinadorStatus | VinculoStatus;

const CONFIG: Record<Status, { label: string; color: "warning" | "success" | "error" | "default" }> = {
  AguardandoPagamento: { label: "Aguardando pagamento", color: "warning" },
  AguardandoAprovacao: { label: "Aguardando", color: "warning" },
  Ativo: { label: "Ativo", color: "success" },
  Inativo: { label: "Inativo", color: "error" },
};

interface StatusChipProps {
  status: Status;
  size?: "small" | "medium";
}

export default function StatusChip({ status, size = "small" }: StatusChipProps) {
  const { label, color } = CONFIG[status];
  return <Chip label={label} color={color} size={size} aria-label={`Status: ${label}`} />;
}
