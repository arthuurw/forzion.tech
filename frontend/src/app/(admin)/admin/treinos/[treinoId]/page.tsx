"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  Box, Typography, Card, Chip, IconButton, Stack,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import { adminApi } from "@/lib/api/admin";
import type { TreinoResponse } from "@/types";
import { OBJETIVO_LABEL } from "@/lib/constants/labels";
import { formatarSeries } from "@/lib/utils/formatting";

const COLUMNS: Column[] = [
  { label: "Exercício" },
  { label: "Séries" },
  { label: "Observação" },
];

export default function DetalheTreinoAdminPage() {
  const { treinoId } = useParams<{ treinoId: string }>();
  const router = useRouter();
  const [treino, setTreino] = useState<TreinoResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await adminApi.getTreino(treinoId);
      setTreino(res.data);
    } catch {
      setError("Erro ao carregar treino.");
    } finally {
      setLoading(false);
    }
  }, [treinoId]);

  useEffect(() => { load(); }, [load]);

  if (loading) return <LoadingSpinner />;

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 3 }}>
        <IconButton onClick={() => router.back()} size="small">
          <ArrowBackIcon />
        </IconButton>
        <Box sx={{ flex: 1 }}>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>{treino?.nome ?? "Treino"}</Typography>
          {treino && (
            <Stack direction="row" spacing={1} sx={{ mt: 0.5 }}>
              <Chip label={OBJETIVO_LABEL[treino.objetivo] ?? treino.objetivo} size="small" />
              {treino.nomeAluno && (
                <Chip label={`Aluno: ${treino.nomeAluno}`} size="small" variant="outlined" />
              )}
            </Stack>
          )}
        </Box>
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      {treino && (
        <>
          <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>
            Exercícios ({treino.exercicios.length})
          </Typography>

          <Card variant="outlined">
            {treino.exercicios.length === 0 ? (
              <EmptyState message="Nenhum exercício nesta ficha." />
            ) : (
              <ResponsiveTable
                columns={COLUMNS}
                rows={treino.exercicios}
                rowKey={(ex) => ex.treinoExercicioId}
                renderCell={(ex, i) => {
                  if (i === 0) return (
                    <Typography variant="body2" sx={{ fontWeight: 500 }}>{ex.nomeExercicio}</Typography>
                  );
                  if (i === 1) return (
                    <Typography variant="body2" sx={{ fontSize: 12, color: "text.secondary" }}>
                      {formatarSeries(ex.series)}
                    </Typography>
                  );
                  return ex.observacao ? (
                    <Typography variant="body2" sx={{ fontSize: 12, color: "text.secondary", maxWidth: 200, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                      {ex.observacao}
                    </Typography>
                  ) : (
                    <Typography variant="body2" sx={{ fontSize: 12, color: "text.disabled" }}>—</Typography>
                  );
                }}
              />
            )}
          </Card>
        </>
      )}
    </Box>
  );
}
