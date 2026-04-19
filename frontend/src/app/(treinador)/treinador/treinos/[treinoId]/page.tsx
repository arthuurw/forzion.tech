"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  Box, Typography, Card, Chip, Stack, Button,
  Table, TableHead, TableRow, TableCell, TableBody,
  Dialog, DialogTitle, DialogContent, DialogActions,
  Autocomplete, TextField, IconButton, Tooltip,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import LinkIcon from "@mui/icons-material/Link";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import { treinadorApi, type AdicionarExercicioData } from "@/lib/api/treinador";
import type { TreinoResponse, TreinoExercicioResponse, ExercicioResponse, AlunoResponse } from "@/types";

export default function DetalheFichaPage() {
  const { treinoId } = useParams<{ treinoId: string }>();
  const router = useRouter();
  const [ficha, setFicha] = useState<TreinoResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  // Adicionar exercício
  const [addOpen, setAddOpen] = useState(false);
  const [biblioteca, setBiblioteca] = useState<ExercicioResponse[]>([]);
  const [selectedEx, setSelectedEx] = useState<ExercicioResponse | null>(null);
  const [series, setSeries] = useState("3");
  const [repeticoes, setRepeticoes] = useState("12");
  const [carga, setCarga] = useState("");
  const [descanso, setDescanso] = useState("60");
  const [loadingAdd, setLoadingAdd] = useState(false);

  // Remover exercício
  const [removeEx, setRemoveEx] = useState<TreinoExercicioResponse | null>(null);
  const [loadingRemove, setLoadingRemove] = useState(false);

  // Duplicar
  const [loadingDuplicar, setLoadingDuplicar] = useState(false);

  // Vincular a aluno
  const [vincularOpen, setVincularOpen] = useState(false);
  const [alunos, setAlunos] = useState<AlunoResponse[]>([]);
  const [selectedAluno, setSelectedAluno] = useState<AlunoResponse | null>(null);
  const [loadingVincular, setLoadingVincular] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await treinadorApi.getFicha(treinoId);
      setFicha(res.data);
    } catch {
      setError("Erro ao carregar ficha.");
    } finally {
      setLoading(false);
    }
  }, [treinoId]);

  useEffect(() => { load(); }, [load]);

  const openAdd = async () => {
    setAddOpen(true);
    setSelectedEx(null); setSeries("3"); setRepeticoes("12"); setCarga(""); setDescanso("60");
    if (biblioteca.length === 0) {
      try {
        const res = await treinadorApi.listExercicios({ global: false, tamanhoPagina: 200 });
        setBiblioteca(res.data.items);
      } catch {
        setError("Erro ao carregar biblioteca de exercícios.");
      }
    }
  };

  const handleAdd = async () => {
    if (!selectedEx) return;
    setLoadingAdd(true);
    try {
      const data: AdicionarExercicioData = {
        exercicioId: selectedEx.exercicioId,
        series: Number(series),
        repeticoes: Number(repeticoes),
        carga: carga ? Number(carga) : null,
        descanso: descanso ? Number(descanso) : null,
      };
      await treinadorApi.adicionarExercicio(treinoId, data);
      setSuccess("Exercício adicionado à ficha.");
      setAddOpen(false);
      load();
    } catch {
      setError("Erro ao adicionar exercício.");
    } finally {
      setLoadingAdd(false);
    }
  };

  const handleRemove = async () => {
    if (!removeEx) return;
    setLoadingRemove(true);
    try {
      await treinadorApi.removerExercicio(treinoId, removeEx.treinoExercicioId);
      setSuccess("Exercício removido.");
      setRemoveEx(null);
      load();
    } catch {
      setError("Erro ao remover exercício.");
    } finally {
      setLoadingRemove(false);
    }
  };

  const handleDuplicar = async () => {
    setLoadingDuplicar(true);
    try {
      const res = await treinadorApi.duplicarFicha(treinoId);
      router.push(`/treinador/treinos/${res.data.treinoId}`);
    } catch {
      setError("Erro ao duplicar ficha.");
      setLoadingDuplicar(false);
    }
  };

  const openVincular = async () => {
    setVincularOpen(true);
    setSelectedAluno(null);
    if (alunos.length === 0) {
      try {
        const res = await treinadorApi.listAlunos({ status: "Ativo", tamanhoPagina: 200 });
        setAlunos(res.data.items);
      } catch {
        setError("Erro ao carregar alunos ativos.");
      }
    }
  };

  const handleVincular = async () => {
    if (!selectedAluno) return;
    setLoadingVincular(true);
    try {
      await treinadorApi.vincularFichaAoAluno(selectedAluno.alunoId, treinoId);
      setSuccess(`Ficha vinculada a ${selectedAluno.nome}.`);
      setVincularOpen(false);
    } catch {
      setError("Erro ao vincular ficha.");
    } finally {
      setLoadingVincular(false);
    }
  };

  if (loading) return <LoadingSpinner />;
  if (!ficha) return null;

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 3 }}>
        <IconButton onClick={() => router.push("/treinador/treinos")} size="small">
          <ArrowBackIcon />
        </IconButton>
        <Box sx={{ flex: 1 }}>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>{ficha.nome}</Typography>
          <Chip label={ficha.objetivo} size="small" sx={{ mt: 0.5 }} />
        </Box>
        <Stack direction="row" spacing={1}>
          <Button
            variant="outlined"
            size="small"
            startIcon={<ContentCopyIcon />}
            disabled={loadingDuplicar}
            onClick={handleDuplicar}
          >
            Duplicar
          </Button>
          <Button
            variant="outlined"
            size="small"
            startIcon={<LinkIcon />}
            onClick={openVincular}
          >
            Vincular aluno
          </Button>
        </Stack>
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
        <Typography variant="h6" sx={{ fontWeight: 600 }}>
          Exercícios ({ficha.exercicios.length})
        </Typography>
        <Button variant="contained" size="small" startIcon={<AddIcon />} onClick={openAdd}>
          Adicionar exercício
        </Button>
      </Box>

      <Card variant="outlined">
        {ficha.exercicios.length === 0 ? (
          <EmptyState
            message="Nenhum exercício nesta ficha ainda."
            actionLabel="Adicionar exercício"
            onAction={openAdd}
          />
        ) : (
          <Box sx={{ overflowX: "auto" }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell sx={{ fontWeight: 600 }}>Exercício</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>Séries</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>Reps</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>Carga (kg)</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>Descanso (s)</TableCell>
                  <TableCell align="right" sx={{ fontWeight: 600 }}>Ações</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {ficha.exercicios.map((ex) => (
                  <TableRow key={ex.treinoExercicioId} hover>
                    <TableCell sx={{ fontWeight: 500 }}>{ex.nomeExercicio}</TableCell>
                    <TableCell>{ex.series}</TableCell>
                    <TableCell>{ex.repeticoes}</TableCell>
                    <TableCell>{ex.carga ?? "—"}</TableCell>
                    <TableCell>{ex.descansoSegundos ?? "—"}</TableCell>
                    <TableCell align="right">
                      <Tooltip title="Remover exercício">
                        <IconButton size="small" color="error" onClick={() => setRemoveEx(ex)}>
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        )}
      </Card>

      {/* Dialog — Adicionar Exercício */}
      <Dialog open={addOpen} onClose={() => setAddOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Adicionar exercício</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <Autocomplete
              options={biblioteca}
              getOptionLabel={(ex) => `${ex.nome}${ex.grupoMuscular ? ` — ${ex.grupoMuscular}` : ""}`}
              value={selectedEx}
              onChange={(_, v) => setSelectedEx(v)}
              renderInput={(params) => (
                <TextField {...params} label="Exercício" size="small" required />
              )}
            />
            <Stack direction="row" spacing={2}>
              <TextField
                label="Séries"
                type="number"
                value={series}
                onChange={(e) => setSeries(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ htmlInput: { min: 1 } }}
              />
              <TextField
                label="Repetições"
                type="number"
                value={repeticoes}
                onChange={(e) => setRepeticoes(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ htmlInput: { min: 1 } }}
              />
            </Stack>
            <Stack direction="row" spacing={2}>
              <TextField
                label="Carga (kg)"
                type="number"
                value={carga}
                onChange={(e) => setCarga(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ htmlInput: { min: 0, step: 0.5 } }}
              />
              <TextField
                label="Descanso (s)"
                type="number"
                value={descanso}
                onChange={(e) => setDescanso(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ htmlInput: { min: 0 } }}
              />
            </Stack>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAddOpen(false)}>Cancelar</Button>
          <Button variant="contained" disabled={!selectedEx || loadingAdd} onClick={handleAdd}>
            Adicionar
          </Button>
        </DialogActions>
      </Dialog>

      {/* ConfirmDialog — Remover */}
      <ConfirmDialog
        open={!!removeEx}
        title="Remover exercício"
        description={`Remover "${removeEx?.nomeExercicio}" desta ficha?`}
        confirmLabel="Remover"
        destructive
        loading={loadingRemove}
        onConfirm={handleRemove}
        onClose={() => setRemoveEx(null)}
      />

      {/* Dialog — Vincular Aluno */}
      <Dialog open={vincularOpen} onClose={() => setVincularOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Vincular ficha a aluno</DialogTitle>
        <DialogContent sx={{ pt: 2 }}>
          <Autocomplete
            options={alunos}
            getOptionLabel={(a) => a.nome}
            value={selectedAluno}
            onChange={(_, v) => setSelectedAluno(v)}
            renderInput={(params) => <TextField {...params} label="Aluno ativo" size="small" />}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setVincularOpen(false)}>Cancelar</Button>
          <Button
            variant="contained"
            disabled={!selectedAluno || loadingVincular}
            onClick={handleVincular}
          >
            Vincular
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
