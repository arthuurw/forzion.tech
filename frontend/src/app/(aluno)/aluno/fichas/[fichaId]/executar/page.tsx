"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  Box, Typography, Card, CardContent, Button, Stack, LinearProgress,
  TextField, IconButton, Chip, Dialog, DialogTitle, DialogContent,
  DialogActions,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import CheckIcon from "@mui/icons-material/Check";
import FitnessCenterIcon from "@mui/icons-material/FitnessCenter";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import AlertBanner from "@/components/ui/AlertBanner";
import { alunoApi, type TreinoAlunoDetalheResponse } from "@/lib/api/aluno";
import type { TreinoExercicioResponse } from "@/types";

export default function ExecutarFichaPage() {
  const { fichaId } = useParams<{ fichaId: string }>();
  const router = useRouter();
  const [ficha, setFicha] = useState<TreinoAlunoDetalheResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [currentIndex, setCurrentIndex] = useState(0);
  const [observacao, setObservacao] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);

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

  const handleSubmit = async () => {
    if (!ficha) return;
    setSubmitting(true);
    try {
      await alunoApi.criarExecucao({
        treinoId: ficha.treinoId,
        dataExecucao: new Date().toISOString(),
        observacao: observacao.trim() || null,
        exercicios: [],
      });
      setDone(true);
      setConfirmOpen(false);
    } catch {
      setError("Erro ao registrar treino.");
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <LoadingSpinner />;
  if (!ficha) return null;

  if (done) {
    return (
      <Box
        sx={{
          minHeight: "60vh",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          gap: 3,
          textAlign: "center",
          px: 2,
        }}
      >
        <Box
          sx={{
            width: 80,
            height: 80,
            borderRadius: "50%",
            bgcolor: "success.main",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
          }}
        >
          <CheckIcon sx={{ color: "white", fontSize: 44 }} />
        </Box>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>Treino concluído!</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            {ficha.nomeTreino} — {ficha.exercicios.length} exercícios
          </Typography>
        </Box>
        <Stack direction="row" spacing={2}>
          <Button variant="outlined" onClick={() => router.push("/aluno/historico")}>
            Ver histórico
          </Button>
          <Button variant="contained" onClick={() => router.push("/aluno/fichas")}>
            Voltar às fichas
          </Button>
        </Stack>
      </Box>
    );
  }

  const exercicios = ficha.exercicios;
  const total = exercicios.length;
  const current: TreinoExercicioResponse | undefined = exercicios[currentIndex];
  const progress = total > 0 ? ((currentIndex) / total) * 100 : 0;
  const isLast = currentIndex === total - 1;

  return (
    <Box>
      {/* Header */}
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 2 }}>
        <IconButton onClick={() => router.push(`/aluno/fichas/${fichaId}`)} size="small">
          <ArrowBackIcon />
        </IconButton>
        <Box sx={{ flex: 1 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>{ficha.nomeTreino}</Typography>
          <Typography variant="caption" color="text.secondary">
            Exercício {currentIndex + 1} de {total}
          </Typography>
        </Box>
        <Chip label={`${currentIndex + 1}/${total}`} size="small" color="primary" />
      </Box>

      <LinearProgress variant="determinate" value={progress} sx={{ mb: 3, borderRadius: 1 }} />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      {/* Exercise card */}
      {current && (
        <Card
          variant="outlined"
          sx={{
            mb: 3,
            borderColor: "primary.main",
            borderWidth: 2,
          }}
        >
          <CardContent sx={{ p: { xs: 3, sm: 4 } }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 3 }}>
              <Box sx={{ p: 1.5, borderRadius: 2, bgcolor: "primary.main" + "20" }}>
                <FitnessCenterIcon sx={{ color: "primary.main", fontSize: 28 }} />
              </Box>
              <Typography variant="h5" sx={{ fontWeight: 700 }}>
                {current.nomeExercicio}
              </Typography>
            </Box>

            <Stack
              direction="row"
              spacing={2}
              sx={{ mb: 2, flexWrap: "wrap" }}
            >
              <Box sx={{ textAlign: "center", minWidth: 80 }}>
                <Typography variant="h3" sx={{ fontWeight: 800, color: "primary.main", lineHeight: 1 }}>
                  {current.series}
                </Typography>
                <Typography variant="caption" color="text.secondary">séries</Typography>
              </Box>
              <Box sx={{ textAlign: "center", minWidth: 80 }}>
                <Typography variant="h3" sx={{ fontWeight: 800, color: "primary.main", lineHeight: 1 }}>
                  {current.repeticoes}
                </Typography>
                <Typography variant="caption" color="text.secondary">repetições</Typography>
              </Box>
              {current.carga != null && (
                <Box sx={{ textAlign: "center", minWidth: 80 }}>
                  <Typography variant="h3" sx={{ fontWeight: 800, lineHeight: 1 }}>
                    {current.carga}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">kg</Typography>
                </Box>
              )}
              {current.descansoSegundos != null && (
                <Box sx={{ textAlign: "center", minWidth: 80 }}>
                  <Typography variant="h3" sx={{ fontWeight: 800, lineHeight: 1 }}>
                    {current.descansoSegundos}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">s descanso</Typography>
                </Box>
              )}
            </Stack>
          </CardContent>
        </Card>
      )}

      {/* Navigation */}
      <Stack direction="row" spacing={2} sx={{ justifyContent: "space-between" }}>
        <Button
          variant="outlined"
          startIcon={<ArrowBackIcon />}
          disabled={currentIndex === 0}
          onClick={() => setCurrentIndex((i) => i - 1)}
        >
          Anterior
        </Button>

        {!isLast ? (
          <Button
            variant="contained"
            endIcon={<ArrowForwardIcon />}
            onClick={() => setCurrentIndex((i) => i + 1)}
          >
            Próximo
          </Button>
        ) : (
          <Button
            variant="contained"
            color="success"
            startIcon={<CheckIcon />}
            onClick={() => setConfirmOpen(true)}
          >
            Finalizar treino
          </Button>
        )}
      </Stack>

      {/* Dots */}
      <Stack direction="row" spacing={0.5} sx={{ justifyContent: "center", mt: 3 }}>
        {exercicios.map((_, i) => (
          <Box
            key={i}
            onClick={() => setCurrentIndex(i)}
            sx={{
              width: 8,
              height: 8,
              borderRadius: "50%",
              bgcolor: i === currentIndex ? "primary.main" : i < currentIndex ? "success.main" : "grey.300",
              cursor: "pointer",
              transition: "background-color 0.2s",
            }}
          />
        ))}
      </Stack>

      {/* Confirm finish dialog */}
      <Dialog open={confirmOpen} onClose={() => setConfirmOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Finalizar treino</DialogTitle>
        <DialogContent>
          <Typography variant="body2" sx={{ mb: 2 }}>
            Parabéns! Você completou todos os exercícios de <strong>{ficha.nomeTreino}</strong>.
          </Typography>
          <TextField
            label="Observação (opcional)"
            value={observacao}
            onChange={(e) => setObservacao(e.target.value)}
            size="small"
            fullWidth
            multiline
            rows={2}
            placeholder="Como foi o treino hoje?"
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmOpen(false)}>Cancelar</Button>
          <Button
            variant="contained"
            color="success"
            disabled={submitting}
            onClick={handleSubmit}
          >
            Registrar treino
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
