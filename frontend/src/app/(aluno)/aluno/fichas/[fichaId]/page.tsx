"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  Box, Typography, Card, CardContent, Chip, Stack, Button, IconButton,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import FitnessCenterIcon from "@mui/icons-material/FitnessCenter";
import TimerOutlinedIcon from "@mui/icons-material/TimerOutlined";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import StatusChip from "@/components/ui/StatusChip";
import { alunoApi, type TreinoAlunoDetalheResponse } from "@/lib/api/aluno";
import { OBJETIVO_LABEL } from "@/lib/constants/labels";
import { exportarFichaParaExcel } from "@/lib/utils/excel";

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

  const totalSeries = ficha.exercicios.reduce(
    (acc, ex) => acc + ex.series.reduce((a, s) => a + s.quantidade, 0), 0,
  );
  const seriesComDescanso = ficha.exercicios.flatMap((ex) => ex.series).filter((s) => s.descanso != null);
  const descansoMedio = seriesComDescanso.length > 0
    ? Math.round(seriesComDescanso.reduce((a, s) => a + (s.descanso ?? 0), 0) / seriesComDescanso.length)
    : null;

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 3 }}>
        <IconButton onClick={() => router.push("/aluno/fichas")} size="small" aria-label="Voltar">
          <ArrowBackIcon />
        </IconButton>
        <Box sx={{ flex: 1 }}>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>{ficha.nomeTreino}</Typography>
          <Stack direction="row" spacing={1} sx={{ mt: 0.5 }}>
            <Chip label={OBJETIVO_LABEL[ficha.objetivo] ?? ficha.objetivo} size="small" />
            <StatusChip status={ficha.status} />
          </Stack>
        </Box>
        <Stack direction="row" spacing={1}>
          <Button
            variant="outlined"
            size="small"
            startIcon={<FileDownloadIcon />}
            onClick={() => void exportarFichaParaExcel({ nome: ficha.nomeTreino, objetivo: ficha.objetivo, exercicios: ficha.exercicios })}
          >
            Exportar
          </Button>
          {ficha.status === "Ativo" && (
            <Button
              variant="contained"
              startIcon={<PlayArrowIcon />}
              onClick={() => router.push(`/aluno/fichas/${fichaId}/executar`)}
            >
              Iniciar treino
            </Button>
          )}
        </Stack>
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <Stack direction="row" spacing={1} sx={{ mb: 3, flexWrap: "wrap", rowGap: 1 }}>
        <Chip
          icon={<FitnessCenterIcon />}
          label={`${ficha.exercicios.length} exercício${ficha.exercicios.length !== 1 ? "s" : ""}`}
          variant="outlined"
        />
        <Chip
          label={`${totalSeries} série${totalSeries !== 1 ? "s" : ""} no total`}
          variant="outlined"
        />
        {descansoMedio !== null && (
          <Chip
            icon={<TimerOutlinedIcon />}
            label={`~${descansoMedio}s descanso médio`}
            variant="outlined"
          />
        )}
      </Stack>

      <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>
        Exercícios
      </Typography>

      {ficha.exercicios.length === 0 ? (
        <EmptyState message="Nenhum exercício nesta ficha." />
      ) : (
        <Stack spacing={2}>
          {[...ficha.exercicios].sort((a, b) => a.ordem - b.ordem).map((ex, idx) => (
            <Card key={ex.treinoExercicioId} variant="outlined">
              <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
                <Stack direction="row" spacing={1.5} sx={{ alignItems: "flex-start" }}>
                  <Box
                    sx={{
                      minWidth: 28, height: 28, borderRadius: "50%",
                      bgcolor: "primary.main", display: "flex",
                      alignItems: "center", justifyContent: "center", mt: 0.25, flexShrink: 0,
                    }}
                  >
                    <Typography variant="caption" sx={{ fontWeight: 700, color: "#1A1A1A", lineHeight: 1 }}>
                      {idx + 1}
                    </Typography>
                  </Box>
                  <Box sx={{ flex: 1 }}>
                    <Typography variant="body1" sx={{ fontWeight: 600, mb: ex.observacao ? 0.75 : 1 }}>
                      {ex.nomeExercicio}
                    </Typography>
                    {ex.observacao && (
                      <Box
                        sx={{
                          display: "flex", alignItems: "flex-start", gap: 0.75, mb: 1,
                          px: 1.25, py: 0.875, borderRadius: 1.5,
                          bgcolor: "primary.main", opacity: 0.9,
                          border: "1px solid", borderColor: "primary.main",
                        }}
                      >
                        <InfoOutlinedIcon sx={{ fontSize: 15, color: "#1A1A1A", mt: "1px", flexShrink: 0 }} />
                        <Typography variant="caption" sx={{ color: "#1A1A1A", fontStyle: "italic", lineHeight: 1.5 }}>
                          {ex.observacao}
                        </Typography>
                      </Box>
                    )}
                    <Stack spacing={0.75}>
                      {[...(ex.series ?? [])].sort((a, b) => a.ordem - b.ordem).map((s, si) => {
                        const reps = s.repeticoesMax
                          ? `${s.repeticoesMin}–${s.repeticoesMax} reps`
                          : `${s.repeticoesMin} reps`;
                        return (
                          <Box
                            key={s.serieConfigId ?? si}
                            sx={{
                              display: "flex", alignItems: "center", gap: 1.5, px: 1.25, py: 1,
                              borderRadius: 1.5, bgcolor: "grey.50", border: "1px solid", borderColor: "divider",
                            }}
                          >
                            <Typography variant="body2" sx={{ fontWeight: 800, color: "primary.main", minWidth: 32, lineHeight: 1 }}>
                              {s.quantidade}×
                            </Typography>
                            <Box sx={{ flex: 1 }}>
                              <Typography variant="body2" sx={{ fontWeight: 600 }}>{reps}</Typography>
                              {s.descricao && (
                                <Typography variant="caption" color="text.secondary">{s.descricao}</Typography>
                              )}
                            </Box>
                            {s.carga != null && (
                              <Chip label={`${s.carga} kg`} size="small" variant="outlined" />
                            )}
                            {s.descanso != null && (
                              <Stack direction="row" spacing={0.5} sx={{ alignItems: "center" }}>
                                <TimerOutlinedIcon sx={{ fontSize: 14, color: "text.secondary" }} />
                                <Typography variant="caption" color="text.secondary">{s.descanso}s</Typography>
                              </Stack>
                            )}
                          </Box>
                        );
                      })}
                    </Stack>
                  </Box>
                </Stack>
              </CardContent>
            </Card>
          ))}
        </Stack>
      )}
    </Box>
  );
}
