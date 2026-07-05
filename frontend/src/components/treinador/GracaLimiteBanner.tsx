"use client";
import dayjs from "dayjs";
import { Button } from "@mui/material";
import Link from "next/link";
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
      action={
        <Button component={Link} href="/treinador/plano" color="warning" size="small">
          Ver planos
        </Button>
      }
    />
  );
}
