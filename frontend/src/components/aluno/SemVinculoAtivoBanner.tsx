"use client";
import { useEffect, useState } from "react";
import { Alert } from "@mui/material";
import { alunoApi } from "@/lib/api/aluno";
import type { VinculoResumo } from "@/types";

type Estado = "ativo" | "pendente" | "sem-vinculo";

function estadoFromResumo(v: VinculoResumo): Estado {
  if (v.ativo) return "ativo";
  if (v.pendente) return "pendente";
  return "sem-vinculo";
}

export default function SemVinculoAtivoBanner({ vinculo }: { vinculo?: VinculoResumo }) {
  const [estado, setEstado] = useState<Estado>(() =>
    vinculo ? estadoFromResumo(vinculo) : "ativo",
  );

  useEffect(() => {
    if (vinculo !== undefined) {
      setEstado(estadoFromResumo(vinculo));
      return;
    }
    let active = true;
    alunoApi.getMeuVinculo()
      .then((res) => {
        if (!active) return;
        if (res.data.vinculoAtivo) setEstado("ativo");
        else if (res.data.vinculoPendente) setEstado("pendente");
        else setEstado("sem-vinculo");
      })
      .catch(() => { if (active) setEstado("ativo"); });
    return () => { active = false; };
  }, [vinculo]);

  if (estado === "ativo") return null;

  return (
    <Alert severity="info" sx={{ mb: 3 }}>
      {estado === "pendente"
        ? "Seu vínculo está aguardando aprovação do treinador. Você pode consultar seu histórico; registrar novos treinos será liberado após a aprovação."
        : "Você não tem um vínculo ativo. Seu histórico continua disponível para consulta, mas o registro de novos treinos fica bloqueado até um novo vínculo."}
    </Alert>
  );
}
