"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  Box, Typography, Card, Chip, Stack, Button, IconButton,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import StatusChip from "@/components/ui/StatusChip";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import { alunoApi, type TreinoAlunoDetalheResponse } from "@/lib/api/aluno";

import { OBJETIVO_LABEL } from "@/lib/constants/labels";

const COLUMNS: Column[] = [
  { label: "#", mobileRole: "hidden" },
  { label: "Exercício", mobileRole: "primary" },
  { label: "Séries" },
];

export default function DetalheFichaAlunoPage() {
  const { fichaId } = useParams<{ fichaId: string }>();
  const router = useRouter();
  const [ficha, setFicha] = useState<TreinoAlunoDetalheResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await alunoApi.getFicha(fichaId);
      setFicha(res.data);
    } catch {
      setError("Erro ao carregar ficha.");
    } finally {
      setLoading(false);
    }
  }, [fichaId]);

  useEffect(() => { load(); }, [load]);

  if (loading) return <LoadingSpinner />;
  if (!ficha) return null;

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 3 }}>
        <IconButton onClick={() => router.push("/aluno/fichas")} size="small">
          <ArrowBackIcon />
        </IconButton>
        <Box sx={{ flex: 1 }}>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>{ficha.nomeTreino}</Typography>
          <Stack direction="row" spacing={1} sx={{ mt: 0.5 }}>
            <Chip label={OBJETIVO_LABEL[ficha.objetivo] ?? ficha.objetivo} size="small" />
            <StatusChip status={ficha.status} />
          </Stack>
        </Box>
        {ficha.status === "Ativo" && (
          <Button
            variant="contained"
            startIcon={<PlayArrowIcon />}
            onClick={() => router.push(`/aluno/fichas/${fichaId}/executar`)}
          >
            Iniciar treino
          </Button>
        )}
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>
        Exercícios ({ficha.exercicios.length})
      </Typography>

      <Card variant="outlined">
        {ficha.exercicios.length === 0 ? (
          <EmptyState message="Nenhum exercício nesta ficha." />
        ) : (
          <ResponsiveTable
            columns={COLUMNS}
            rows={ficha.exercicios}
            rowKey={(ex) => ex.treinoExercicioId}
            renderCell={(ex, i, rowIndex) => {
              if (i === 0) return (
                <Typography variant="caption" color="text.secondary">{rowIndex + 1}</Typography>
              );
              if (i === 1) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{ex.nomeExercicio}</Typography>;
              return (
                <Typography variant="body2" sx={{ fontSize: 12, color: "text.secondary" }}>
                  {ex.series.length === 0 ? "—" : ex.series.map((s) => {
                    const reps = s.repeticoesMax ? `${s.repeticoesMin}–${s.repeticoesMax}` : `${s.repeticoesMin}`;
                    return `${s.quantidade}×${reps}${s.descricao ? ` (${s.descricao})` : ""}`;
                  }).join(" / ")}
                </Typography>
              );
            }}
          />
        )}
      </Card>
    </Box>
  );
}
