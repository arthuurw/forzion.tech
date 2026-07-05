"use client";
import dayjs from "dayjs";
import AlertBanner from "@/components/ui/AlertBanner";

interface GracaLimiteBannerProps {
  gracaAte: string | null;
  excedente: number;
}

export default function GracaLimiteBanner({ gracaAte, excedente }: GracaLimiteBannerProps) {
  if (!gracaAte) return null;

  return (
    <AlertBanner
      open
      severity="warning"
      title="Limite de alunos do seu plano excedido"
      message={`Faltam inativar ${excedente} aluno(s) até ${dayjs(gracaAte).format("DD/MM/YYYY")} para se adequar ao limite do seu plano.`}
    />
  );
}
