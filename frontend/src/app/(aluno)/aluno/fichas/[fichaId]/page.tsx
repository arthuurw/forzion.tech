"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  Box, Typography, Card, Chip, Stack, Button,
  Table, TableHead, TableRow, TableCell, TableBody, IconButton,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import StatusChip from "@/components/ui/StatusChip";
import { alunoApi, type TreinoAlunoDetalheResponse } from "@/lib/api/aluno";

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
            <Chip label={ficha.objetivo} size="small" />
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
          <Box sx={{ overflowX: "auto" }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell sx={{ fontWeight: 600 }}>#</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>Exercício</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>Séries</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>Reps</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>Carga (kg)</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>Descanso (s)</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {ficha.exercicios.map((ex, i) => (
                  <TableRow key={ex.treinoExercicioId} hover>
                    <TableCell>
                      <Typography variant="caption" color="text.secondary">{i + 1}</Typography>
                    </TableCell>
                    <TableCell sx={{ fontWeight: 500 }}>{ex.nomeExercicio}</TableCell>
                    <TableCell>{ex.series}</TableCell>
                    <TableCell>{ex.repeticoes}</TableCell>
                    <TableCell>{ex.carga ?? "—"}</TableCell>
                    <TableCell>{ex.descansoSegundos ?? "—"}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        )}
      </Card>
    </Box>
  );
}
