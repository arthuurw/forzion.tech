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
import { isAxiosError } from "axios";
import { alunoApi, type TreinoAlunoDetalheResponse } from "@/lib/api/aluno";
import type { ProblemDetails, TreinoExercicioResponse } from "@/types";

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
    } catch (err) {
      if (isAxiosError(err) && err.response) {
        const problem = err.response.data as ProblemDetails;
        if (problem.status === 404) setError("Ficha não encontrada.");
        else if (problem.status === 422) setError(problem.detail ?? "Dados inválidos para registrar o treino.");
        else setError("Erro ao registrar treino. Tente novamente.");
      } else {
        setError("Erro ao registrar treino.");
      }
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
          <Typography variant="h5" sx={{ fontWeight: 700 }}>Sessão registrada</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            {ficha.nomeTreino} — {ficha.exercicios.length} exercício(s) concluído(s)
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

            <Stack spacing={1} sx={{ mb: 2 }}>
              {current.series.map((s, i) => {
                const reps = s.repeticoesMax
                  ? `${s.repeticoesMin}–${s.repeticoesMax} reps`
                  : `${s.repeticoesMin} reps`;
                return (
                  <Box
                    key={i}
                    sx={{
                      display: "flex",
                      alignItems: "center",
                      gap: 1.5,
                      p: 1.5,
                      borderRadius: 2,
                      bgcolor: "grey.50",
                      border: "1px solid",
                      borderColor: "divider",
                    }}
                  >
                    <Typography variant="h5" sx={{ fontWeight: 800, color: "primary.main", minWidth: 36, lineHeight: 1 }}>
                      {s.quantidade}×
                    </Typography>
                    <Box sx={{ flex: 1 }}>
                      <Typography variant="body1" sx={{ fontWeight: 600 }}>{reps}</Typography>
                      {s.descricao && (
                        <Typography variant="caption" color="text.secondary">{s.descricao}</Typography>
                      )}
                    </Box>
                    {s.carga != null && (
                      <Typography variant="body2" color="text.secondary">{s.carga} kg</Typography>
                    )}
                    {s.descanso != null && (
                      <Typography variant="body2" color="text.secondary">{s.descanso}s</Typography>
                    )}
                  </Box>
                );
              })}
            </Stack>
          </CardContent>
        </Card>
      )}

      {/* Navigation */}
      <Stack direction="row" spacing={2} sx={{ justifyContent: "space-between" }}>
        <Button
          variant="outlined"
          startIcon={<ArrowBackIcon />}
          disabled={currentIndex === 0 || submitting}
          onClick={() => setCurrentIndex((i) => i - 1)}
        >
          Anterior
        </Button>

        {!isLast ? (
          <Button
            variant="contained"
            endIcon={<ArrowForwardIcon />}
            disabled={submitting}
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
            onClick={() => !submitting && setCurrentIndex(i)}
            sx={{
              width: 8,
              height: 8,
              borderRadius: "50%",
              bgcolor: i === currentIndex ? "primary.main" : i < currentIndex ? "success.main" : "grey.300",
              cursor: submitting ? "default" : "pointer",
              transition: "background-color 0.2s",
              opacity: submitting ? 0.5 : 1,
            }}
          />
        ))}
      </Stack>

      {/* Confirm finish dialog */}
      <Dialog open={confirmOpen} onClose={() => setConfirmOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Registrar sessão</DialogTitle>
        <DialogContent>
          <Typography variant="body2" sx={{ mb: 2 }}>
            Todos os exercícios de <strong>{ficha.nomeTreino}</strong> foram concluídos. Adicione uma observação antes de registrar.
          </Typography>
          <TextField
            label="Observação (opcional)"
            value={observacao}
            onChange={(e) => setObservacao(e.target.value)}
            size="small"
            fullWidth
            multiline
            rows={2}
            placeholder="Desempenho, ajustes de carga, sensações..."
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
            Confirmar registro
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
