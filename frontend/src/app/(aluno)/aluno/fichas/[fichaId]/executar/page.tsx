"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  Box, Typography, Card, CardContent, Button, Stack, LinearProgress,
  TextField, IconButton, Chip, Dialog, DialogTitle, DialogContent,
  DialogActions, Divider,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import CheckIcon from "@mui/icons-material/Check";
import FitnessCenterIcon from "@mui/icons-material/FitnessCenter";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import AlertBanner from "@/components/ui/AlertBanner";
import { isAxiosError } from "axios";
import { alunoApi, type TreinoAlunoDetalheResponse, type ExecucaoExercicioData } from "@/lib/api/aluno";
import type { ProblemDetails, TreinoExercicioResponse } from "@/types";

type SetState = { reps: string; carga: string; groupLabel?: string };

function initExecData(exercicios: TreinoExercicioResponse[]): Record<string, SetState[]> {
  const map: Record<string, SetState[]> = {};
  for (const ex of exercicios) {
    const sets: SetState[] = [];
    for (const s of [...(ex.series ?? [])].sort((a, b) => a.ordem - b.ordem)) {
      for (let i = 0; i < s.quantidade; i++) {
        sets.push({
          reps: String(s.repeticoesMin),
          carga: s.carga != null ? String(s.carga) : "",
          groupLabel: s.descricao || undefined,
        });
      }
    }
    map[ex.treinoExercicioId] = sets;
  }
  return map;
}

function buildExecPayload(
  exercicios: TreinoExercicioResponse[],
  execData: Record<string, SetState[]>,
  obsData: Record<string, string>,
): ExecucaoExercicioData[] {
  return exercicios.map((ex) => {
    const sets = execData[ex.treinoExercicioId] ?? [];
    const filled = sets.filter((s) => parseInt(s.reps) > 0);
    const avgReps = filled.length > 0
      ? Math.round(filled.reduce((a, s) => a + (parseInt(s.reps) || 0), 0) / filled.length)
      : 0;
    const cargas = filled.map((s) => parseFloat(s.carga)).filter((c) => !isNaN(c) && c > 0);
    const avgCarga = cargas.length > 0 ? cargas.reduce((a, c) => a + c, 0) / cargas.length : null;
    return {
      treinoExercicioId: ex.treinoExercicioId,
      seriesExecutadas: filled.length,
      repeticoesExecutadas: avgReps,
      cargaExecutada: avgCarga,
      observacao: obsData[ex.treinoExercicioId]?.trim() || null,
    };
  });
}

export default function ExecutarFichaPage() {
  const { fichaId } = useParams<{ fichaId: string }>();
  const router = useRouter();
  const [ficha, setFicha] = useState<TreinoAlunoDetalheResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [currentIndex, setCurrentIndex] = useState(0);
  const [execData, setExecData] = useState<Record<string, SetState[]>>({});
  const [obsData, setObsData] = useState<Record<string, string>>({});
  const [observacao, setObservacao] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await alunoApi.getFicha(fichaId);
      const sorted = { ...res.data, exercicios: [...res.data.exercicios].sort((a, b) => a.ordem - b.ordem) };
      setFicha(sorted);
      setExecData(initExecData(sorted.exercicios));
    } catch {
      setError("Erro ao carregar ficha.");
    } finally {
      setLoading(false);
    }
  }, [fichaId]);

  useEffect(() => { load(); }, [load]);

  const setSetField = (exId: string, idx: number, field: keyof SetState, value: string) =>
    setExecData((prev) => {
      const sets = [...(prev[exId] ?? [])];
      sets[idx] = { ...sets[idx], [field]: value };
      return { ...prev, [exId]: sets };
    });

  const setExObs = (exId: string, value: string) =>
    setObsData((prev) => ({ ...prev, [exId]: value }));

  const handleSubmit = async () => {
    if (!ficha) return;
    setSubmitting(true);
    try {
      await alunoApi.criarExecucao({
        treinoId: ficha.treinoId,
        dataExecucao: new Date().toISOString(),
        observacao: observacao.trim() || null,
        exercicios: buildExecPayload(ficha.exercicios, execData, obsData),
      });
      setDone(true);
      setConfirmOpen(false);
    } catch (err) {
      if (isAxiosError(err) && err.response) {
        const problem = err.response.data as ProblemDetails;
        if (problem.status === 404) setError("Ficha não encontrada.");
        else if (problem.status === 422) setError(problem.detail ?? "Dados inválidos para registrar o treino.");
        else if (problem.status === 403) setError("Você não tem um treinador ativo. Não é possível registrar novos treinos.");
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
      <Box sx={{ minHeight: "60vh", display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 3, textAlign: "center", px: 2 }}>
        <Box sx={{ width: 80, height: 80, borderRadius: "50%", bgcolor: "success.main", display: "flex", alignItems: "center", justifyContent: "center" }}>
          <CheckIcon sx={{ color: "white", fontSize: 44 }} />
        </Box>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>Sessão registrada</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            {ficha.nomeTreino} — {ficha.exercicios.length} exercício(s) concluído(s)
          </Typography>
        </Box>
        <Stack direction="row" spacing={2}>
          <Button variant="outlined" onClick={() => router.push("/aluno/historico")}>Ver histórico</Button>
          <Button variant="contained" onClick={() => router.push("/aluno/fichas")}>Voltar às fichas</Button>
        </Stack>
      </Box>
    );
  }

  const exercicios = ficha.exercicios;
  const total = exercicios.length;
  const current: TreinoExercicioResponse | undefined = exercicios[currentIndex];
  const progress = total > 0 ? (currentIndex / total) * 100 : 0;
  const isLast = currentIndex === total - 1;
  const currentSets = current ? (execData[current.treinoExercicioId] ?? []) : [];

  return (
    <Box>
      {/* Header */}
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 2 }}>
        <IconButton onClick={() => router.push(`/aluno/fichas/${fichaId}`)} aria-label="Voltar">
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

      {current && (
        <Card variant="outlined" sx={{ mb: 3, borderColor: "primary.main", borderWidth: 2 }}>
          <CardContent sx={{ p: { xs: 2.5, sm: 3 } }}>
            {/* Exercise name */}
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 2.5 }}>
              <Box sx={{ p: 1.5, borderRadius: 2, bgcolor: "primary.main" + "20" }}>
                <FitnessCenterIcon sx={{ color: "primary.main", fontSize: 28 }} />
              </Box>
              <Typography variant="h5" sx={{ fontWeight: 700 }}>{current.nomeExercicio}</Typography>
            </Box>

            {/* Trainer note */}
            {current.observacao && (
              <Box
                sx={{
                  display: "flex", alignItems: "flex-start", gap: 1, mb: 2.5,
                  px: 1.5, py: 1.25, borderRadius: 2,
                  bgcolor: "primary.main", opacity: 0.9,
                  border: "1px solid", borderColor: "primary.main",
                }}
              >
                <InfoOutlinedIcon sx={{ fontSize: 16, color: "#1A1A1A", mt: "1px", flexShrink: 0 }} />
                <Typography variant="body2" sx={{ color: "#1A1A1A", fontStyle: "italic", lineHeight: 1.5 }}>
                  {current.observacao}
                </Typography>
              </Box>
            )}

            {/* Planned */}
            {(current.series ?? []).length > 0 && (
              <>
                <Typography variant="overline" color="text.secondary" sx={{ display: "block", mb: 1 }}>
                  Planejado
                </Typography>
                <Stack spacing={0.75} sx={{ mb: 2.5 }}>
                  {current.series.map((s, i) => {
                    const reps = s.repeticoesMax
                      ? `${s.repeticoesMin}–${s.repeticoesMax} reps`
                      : `${s.repeticoesMin} reps`;
                    return (
                      <Box
                        key={i}
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
                          <Typography variant="caption" color="text.secondary">{s.descanso}s descanso</Typography>
                        )}
                      </Box>
                    );
                  })}
                </Stack>
              </>
            )}

            <Divider sx={{ mb: 2.5 }} />

            {/* Executed — one row per individual set */}
            <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, mb: 1.5 }}>
              <Typography variant="overline" color="text.secondary" sx={{ display: "block" }}>
                Executado
              </Typography>
              <Box
                component="span"
                title="Reps e carga são registrados como média das séries preenchidas por exercício."
                sx={{ display: "flex", alignItems: "center", cursor: "help" }}
              >
                <InfoOutlinedIcon sx={{ fontSize: 14, color: "text.secondary" }} />
              </Box>
            </Box>
            <Typography
              variant="caption"
              color="text.secondary"
              data-testid="exec-aggregation-hint"
              sx={{ display: "block", mb: 1 }}
            >
              Valores registrados como média por exercício.
            </Typography>
            <Stack spacing={0.75}>
              {currentSets.map((set, idx) => {
                const showGroupHeader = set.groupLabel && set.groupLabel !== currentSets[idx - 1]?.groupLabel;
                return (
                  <Box key={idx}>
                    {showGroupHeader && (
                      <Typography
                        variant="caption"
                        sx={{
                          display: "block", fontWeight: 700, textTransform: "uppercase",
                          letterSpacing: 0.8, color: "primary.main",
                          mt: idx === 0 ? 0 : 1, mb: 0.5,
                        }}
                      >
                        {set.groupLabel}
                      </Typography>
                    )}
                    <Box
                      sx={{
                        display: "flex", alignItems: "center", gap: 1, px: 1.25, py: 0.75,
                        borderRadius: 1.5, bgcolor: "grey.50", border: "1px solid", borderColor: "divider",
                      }}
                    >
                      <Typography variant="body2" sx={{ fontWeight: 800, color: "primary.main", minWidth: 32, lineHeight: 1, flexShrink: 0 }}>
                        {idx + 1}×
                      </Typography>
                      <TextField
                        label="Reps"
                        type="number"
                        size="small"
                        value={set.reps}
                        onChange={(e) => setSetField(current.treinoExercicioId, idx, "reps", e.target.value)}
                        slotProps={{ htmlInput: { min: 0 } }}
                        sx={{ flex: 1, minWidth: 68, maxWidth: 90 }}
                      />
                      <TextField
                        label="kg"
                        type="number"
                        size="small"
                        value={set.carga}
                        onChange={(e) => setSetField(current.treinoExercicioId, idx, "carga", e.target.value)}
                        slotProps={{ htmlInput: { min: 0, step: 0.5 } }}
                        sx={{ flex: 1, minWidth: 68, maxWidth: 90 }}
                      />
                    </Box>
                  </Box>
                );
              })}
              {currentSets.length === 0 && (
                <Typography variant="body2" color="text.secondary">Nenhuma série planejada.</Typography>
              )}
              {currentSets.length > 0 && (
                <TextField
                  label="Observação do exercício (opcional)"
                  size="small"
                  fullWidth
                  value={obsData[current.treinoExercicioId] ?? ""}
                  onChange={(e) => setExObs(current.treinoExercicioId, e.target.value)}
                  placeholder="Ajuste de carga, sensação, dificuldade..."
                  sx={{ mt: 0.5 }}
                />
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
          disabled={currentIndex === 0 || submitting}
          onClick={() => setCurrentIndex((i) => i - 1)}
        >
          Anterior
        </Button>
        {!isLast ? (
          <Button variant="contained" endIcon={<ArrowForwardIcon />} disabled={submitting} onClick={() => setCurrentIndex((i) => i + 1)}>
            Próximo
          </Button>
        ) : (
          <Button variant="contained" color="success" startIcon={<CheckIcon />} onClick={() => setConfirmOpen(true)}>
            Finalizar treino
          </Button>
        )}
      </Stack>

      {/* Progress dots */}
      <Stack direction="row" spacing={0.5} sx={{ justifyContent: "center", mt: 3 }}>
        {exercicios.map((_, i) => (
          <Box
            key={i}
            onClick={() => !submitting && setCurrentIndex(i)}
            sx={{
              width: 8, height: 8, borderRadius: "50%",
              bgcolor: i === currentIndex ? "primary.main" : i < currentIndex ? "success.main" : "grey.300",
              cursor: submitting ? "default" : "pointer",
              transition: "background-color 0.2s",
              opacity: submitting ? 0.5 : 1,
            }}
          />
        ))}
      </Stack>

      {/* Confirm dialog */}
      <Dialog open={confirmOpen} onClose={() => setConfirmOpen(false)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Registrar sessão</DialogTitle>
        <DialogContent>
          <Typography variant="body2" sx={{ mb: 2 }}>
            Todos os exercícios de <strong>{ficha.nomeTreino}</strong> foram concluídos. Adicione uma observação geral antes de registrar.
          </Typography>
          <TextField
            label="Observação geral (opcional)"
            value={observacao}
            onChange={(e) => setObservacao(e.target.value)}
            size="small"
            fullWidth
            multiline
            rows={2}
            placeholder="Desempenho geral, ajustes futuros..."
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmOpen(false)}>Cancelar</Button>
          <Button variant="contained" color="success" disabled={submitting} onClick={handleSubmit}>
            Confirmar registro
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
