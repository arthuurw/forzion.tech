"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  Box, Typography, Card, Chip, Stack, Button, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions,
  Autocomplete, TextField, IconButton, Tooltip, Divider,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import LinkIcon from "@mui/icons-material/Link";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import { treinadorApi, type AdicionarExercicioData, type SerieConfigData } from "@/lib/api/treinador";
import type { TreinoResponse, TreinoExercicioResponse, ExercicioResponse, AlunoResponse, ObjetivoTreino } from "@/types";

const COLUMNS: Column[] = [
  { label: "Exercício" },
  { label: "Séries" },
  { label: "Ações", align: "right" },
];

const OBJETIVOS: ObjetivoTreino[] = ["Hipertrofia", "Emagrecimento", "Resistencia", "Forca", "Flexibilidade", "Condicionamento"];

function formatarSeries(series: TreinoExercicioResponse["series"]): string {
  if (!series || series.length === 0) return "—";
  return series
    .map((s) => {
      const reps = s.repeticoesMax ? `${s.repeticoesMin}–${s.repeticoesMax}` : `${s.repeticoesMin}`;
      const label = s.descricao ? ` (${s.descricao})` : "";
      return `${s.quantidade}×${reps}${label}`;
    })
    .join(" / ");
}

interface SerieRow {
  quantidade: string;
  repeticoesMin: string;
  repeticoesMax: string;
  descricao: string;
  carga: string;
  descanso: string;
}

const SERIE_VAZIA: SerieRow = { quantidade: "3", repeticoesMin: "10", repeticoesMax: "12", descricao: "", carga: "", descanso: "60" };

export default function DetalheFichaPage() {
  const { treinoId } = useParams<{ treinoId: string }>();
  const router = useRouter();
  const [ficha, setFicha] = useState<TreinoResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [addOpen, setAddOpen] = useState(false);
  const [biblioteca, setBiblioteca] = useState<ExercicioResponse[]>([]);
  const [selectedEx, setSelectedEx] = useState<ExercicioResponse | null>(null);
  const [seriesRows, setSeriesRows] = useState<SerieRow[]>([{ ...SERIE_VAZIA }]);
  const [loadingAdd, setLoadingAdd] = useState(false);

  const [removeEx, setRemoveEx] = useState<TreinoExercicioResponse | null>(null);
  const [loadingRemove, setLoadingRemove] = useState(false);

  const [loadingDuplicar, setLoadingDuplicar] = useState(false);

  const [vincularOpen, setVincularOpen] = useState(false);
  const [alunos, setAlunos] = useState<AlunoResponse[]>([]);
  const [selectedAluno, setSelectedAluno] = useState<AlunoResponse | null>(null);
  const [loadingVincular, setLoadingVincular] = useState(false);

  const [editOpen, setEditOpen] = useState(false);
  const [editNome, setEditNome] = useState("");
  const [editObjetivo, setEditObjetivo] = useState<ObjetivoTreino>("Hipertrofia");
  const [loadingEdit, setLoadingEdit] = useState(false);

  const [deleteOpen, setDeleteOpen] = useState(false);
  const [loadingDelete, setLoadingDelete] = useState(false);

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
    setSelectedEx(null);
    setSeriesRows([{ ...SERIE_VAZIA }]);
    if (biblioteca.length === 0) {
      try {
        const res = await treinadorApi.listExercicios({ global: false, tamanhoPagina: 200 });
        setBiblioteca(res.data.items);
      } catch {
        setError("Erro ao carregar biblioteca de exercícios.");
      }
    }
  };

  const updateSerieRow = (idx: number, field: keyof SerieRow, value: string) => {
    setSeriesRows((prev) => prev.map((r, i) => i === idx ? { ...r, [field]: value } : r));
  };

  const addSerieRow = () => setSeriesRows((prev) => [...prev, { ...SERIE_VAZIA, descricao: "" }]);
  const removeSerieRow = (idx: number) => setSeriesRows((prev) => prev.filter((_, i) => i !== idx));

  const handleAdd = async () => {
    if (!selectedEx) return;
    setLoadingAdd(true);
    try {
      const series: SerieConfigData[] = seriesRows.map((r) => ({
        quantidade: Number(r.quantidade) || 1,
        repeticoesMin: Number(r.repeticoesMin) || 1,
        repeticoesMax: r.repeticoesMax ? Number(r.repeticoesMax) : null,
        descricao: r.descricao.trim() || null,
        carga: r.carga ? Number(r.carga) : null,
        descanso: r.descanso ? Number(r.descanso) : null,
      }));
      const data: AdicionarExercicioData = { exercicioId: selectedEx.exercicioId, series };
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
    try {
      const res = await treinadorApi.listAlunosVinculados(treinoId);
      if (res.data.length > 0) {
        const nomes = res.data.map((v) => v.nomeAluno).join(", ");
        setError(`Já existe o aluno ${nomes} vinculado a esta ficha.`);
        return;
      }
    } catch {
      setError("Erro ao verificar vínculos da ficha.");
      return;
    }
    setSelectedAluno(null);
    setVincularOpen(true);
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

  const openEdit = () => {
    if (!ficha) return;
    setEditNome(ficha.nome);
    setEditObjetivo(ficha.objetivo);
    setEditOpen(true);
  };

  const handleEditar = async () => {
    setLoadingEdit(true);
    try {
      await treinadorApi.atualizarFicha(treinoId, { nome: editNome.trim(), objetivo: editObjetivo });
      setEditOpen(false);
      setSuccess("Ficha atualizada.");
      load();
    } catch {
      setError("Erro ao atualizar ficha.");
    } finally {
      setLoadingEdit(false);
    }
  };

  const handleExcluir = async () => {
    setLoadingDelete(true);
    try {
      await treinadorApi.excluirFicha(treinoId);
      router.push("/treinador/treinos");
    } catch {
      setError("Erro ao excluir ficha. Fichas com execuções registradas não podem ser excluídas.");
      setDeleteOpen(false);
      setLoadingDelete(false);
    }
  };

  const canAdd = !!selectedEx && seriesRows.every(
    (r) => Number(r.quantidade) >= 1 && Number(r.repeticoesMin) >= 1
  );

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
          <Button variant="outlined" size="small" startIcon={<ContentCopyIcon />} disabled={loadingDuplicar} onClick={handleDuplicar}>
            Duplicar
          </Button>
          <Button variant="outlined" size="small" startIcon={<LinkIcon />} onClick={openVincular}>
            Vincular aluno
          </Button>
          <Button variant="outlined" size="small" startIcon={<EditIcon />} onClick={openEdit}>
            Editar
          </Button>
          <Button variant="outlined" size="small" color="error" startIcon={<DeleteIcon />} onClick={() => setDeleteOpen(true)}>
            Excluir
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
          <EmptyState message="Nenhum exercício nesta ficha ainda." actionLabel="Adicionar exercício" onAction={openAdd} />
        ) : (
          <ResponsiveTable
            columns={COLUMNS}
            rows={ficha.exercicios}
            rowKey={(ex) => ex.treinoExercicioId}
            renderCell={(ex, i) => {
              if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{ex.nomeExercicio}</Typography>;
              if (i === 1) return (
                <Typography variant="body2" sx={{ fontSize: 12, color: "text.secondary" }}>
                  {formatarSeries(ex.series)}
                </Typography>
              );
              return (
                <Tooltip title="Remover exercício">
                  <IconButton size="small" color="error" onClick={() => setRemoveEx(ex)}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              );
            }}
          />
        )}
      </Card>

      {/* Dialog: adicionar exercício */}
      <Dialog open={addOpen} onClose={() => setAddOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>Adicionar exercício</DialogTitle>
        <DialogContent>
          <Stack spacing={2.5} sx={{ pt: 1 }}>
            <Autocomplete
              options={biblioteca}
              getOptionLabel={(ex) => `${ex.nome}${ex.grupoMuscular ? ` — ${ex.grupoMuscular}` : ""}`}
              value={selectedEx}
              onChange={(_, v) => setSelectedEx(v)}
              renderInput={(params) => <TextField {...params} label="Exercício" size="small" required />}
            />

            <Box>
              <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 600 }}>
                Grupos de séries
              </Typography>
              <Stack spacing={1.5} divider={<Divider />}>
                {seriesRows.map((row, idx) => (
                  <Box key={idx}>
                    <Stack direction="row" sx={{ alignItems: "center", gap: 1, flexWrap: "wrap" }}>
                      <TextField
                        label="Qtd séries"
                        type="number"
                        value={row.quantidade}
                        onChange={(e) => updateSerieRow(idx, "quantidade", e.target.value)}
                        size="small"
                        sx={{ width: 90 }}
                        slotProps={{ htmlInput: { min: 1 } }}
                      />
                      <TextField
                        label="Reps mín."
                        type="number"
                        value={row.repeticoesMin}
                        onChange={(e) => updateSerieRow(idx, "repeticoesMin", e.target.value)}
                        size="small"
                        sx={{ width: 90 }}
                        slotProps={{ htmlInput: { min: 1 } }}
                      />
                      <TextField
                        label="Reps máx."
                        type="number"
                        value={row.repeticoesMax}
                        onChange={(e) => updateSerieRow(idx, "repeticoesMax", e.target.value)}
                        size="small"
                        sx={{ width: 90 }}
                        slotProps={{ htmlInput: { min: 1 } }}
                        helperText="Opcional"
                      />
                      <TextField
                        label="Descrição"
                        value={row.descricao}
                        onChange={(e) => updateSerieRow(idx, "descricao", e.target.value)}
                        size="small"
                        sx={{ flex: 1, minWidth: 120 }}
                        placeholder="Ex.: Aquecimento"
                        slotProps={{ htmlInput: { maxLength: 100 } }}
                      />
                      <TextField
                        label="Carga (kg)"
                        type="number"
                        value={row.carga}
                        onChange={(e) => updateSerieRow(idx, "carga", e.target.value)}
                        size="small"
                        sx={{ width: 90 }}
                        slotProps={{ htmlInput: { min: 0, step: 0.5 } }}
                      />
                      <TextField
                        label="Descanso (s)"
                        type="number"
                        value={row.descanso}
                        onChange={(e) => updateSerieRow(idx, "descanso", e.target.value)}
                        size="small"
                        sx={{ width: 100 }}
                        slotProps={{ htmlInput: { min: 0 } }}
                      />
                      {seriesRows.length > 1 && (
                        <Tooltip title="Remover grupo">
                          <IconButton size="small" color="error" onClick={() => removeSerieRow(idx)}>
                            <DeleteIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}
                    </Stack>
                  </Box>
                ))}
              </Stack>
              <Button
                startIcon={<AddIcon />}
                size="small"
                onClick={addSerieRow}
                sx={{ mt: 1.5 }}
              >
                Adicionar grupo de séries
              </Button>
            </Box>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAddOpen(false)}>Cancelar</Button>
          <Button variant="contained" disabled={!canAdd || loadingAdd} onClick={handleAdd}>
            Adicionar
          </Button>
        </DialogActions>
      </Dialog>

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

      {/* Dialog: editar ficha */}
      <Dialog open={editOpen} onClose={() => setEditOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Editar ficha</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField
              label="Nome"
              value={editNome}
              onChange={(e) => setEditNome(e.target.value)}
              size="small"
              fullWidth
              required
              autoFocus
            />
            <TextField
              select
              label="Objetivo"
              value={editObjetivo}
              onChange={(e) => setEditObjetivo(e.target.value as ObjetivoTreino)}
              size="small"
              fullWidth
            >
              {OBJETIVOS.map((o) => <MenuItem key={o} value={o}>{o}</MenuItem>)}
            </TextField>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditOpen(false)}>Cancelar</Button>
          <Button variant="contained" disabled={!editNome.trim() || loadingEdit} onClick={handleEditar}>
            Salvar
          </Button>
        </DialogActions>
      </Dialog>

      <ConfirmDialog
        open={deleteOpen}
        title="Excluir ficha"
        description={`Excluir "${ficha.nome}"? Fichas com execuções registradas não podem ser excluídas.`}
        confirmLabel="Excluir"
        destructive
        loading={loadingDelete}
        onConfirm={handleExcluir}
        onClose={() => setDeleteOpen(false)}
      />

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
          <Button variant="contained" disabled={!selectedAluno || loadingVincular} onClick={handleVincular}>
            Vincular
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
