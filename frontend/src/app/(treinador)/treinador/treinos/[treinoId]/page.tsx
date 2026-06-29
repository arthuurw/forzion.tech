"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  Box, Typography, Card, Chip, Stack, Button, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions,
  Autocomplete, TextField, IconButton, Tooltip, Divider,
  useMediaQuery, useTheme,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import LinkIcon from "@mui/icons-material/Link";
import NoteAltIcon from "@mui/icons-material/NoteAlt";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import DetalheErro from "@/components/ui/DetalheErro";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import { treinadorApi, type AdicionarExercicioData, type SerieConfigData } from "@/lib/api/treinador";
import { extractApiError } from "@/lib/api/extractApiError";
import type { TreinoResponse, TreinoExercicioResponse, ExercicioResponse, AlunoResponse, ObjetivoTreino } from "@/types";
import { OBJETIVO_LABEL, GRUPO_MUSCULAR_LABEL, OBJETIVOS } from "@/lib/constants/labels";
import { formatarSeries } from "@/lib/utils/formatting";
import { exportarFichaParaExcel } from "@/lib/utils/excel";
import { MAX_PAGE_SIZE } from "@/lib/constants/pagination";

const COLUMNS: Column[] = [
  { label: "Exercício" },
  { label: "Séries" },
  { label: "Observação" },
  { label: "Ações", align: "right" },
];

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
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const [ficha, setFicha] = useState<TreinoResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [addOpen, setAddOpen] = useState(false);
  const [exOptions, setExOptions] = useState<ExercicioResponse[]>([]);
  const [exLoading, setExLoading] = useState(false);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const selectedExRef = useRef<ExercicioResponse | null>(null);
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

  const [obsTarget, setObsTarget] = useState<TreinoExercicioResponse | null>(null);
  const [obsText, setObsText] = useState("");
  const [loadingObs, setLoadingObs] = useState(false);

  const [editExTarget, setEditExTarget] = useState<TreinoExercicioResponse | null>(null);
  const [editSeriesRows, setEditSeriesRows] = useState<SerieRow[]>([]);
  const [loadingEditEx, setLoadingEditEx] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await treinadorApi.getFicha(treinoId);
      setFicha(res.data);
    } catch (err) {
      setError(extractApiError(err, "Erro ao carregar ficha."));
    } finally {
      setLoading(false);
    }
  }, [treinoId]);

  useEffect(() => { load(); }, [load]);

  useEffect(() => () => { if (debounceRef.current) clearTimeout(debounceRef.current); }, []);

  const openAdd = () => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    setAddOpen(true);
    setSelectedEx(null);
    selectedExRef.current = null;
    setSeriesRows([{ ...SERIE_VAZIA }]);
    setExOptions([]);
  };

  const handleExInputChange = (_: unknown, value: string, reason: string) => {
    if (reason !== "input") return;
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(async () => {
      setExLoading(true);
      try {
        const res = await treinadorApi.listExercicios({ nome: value || undefined, global: false, pagina: 1, tamanhoPagina: 20 });
        const items = res.data.items;
        const cur = selectedExRef.current;
        setExOptions(
          cur && !items.some((it) => it.exercicioId === cur.exercicioId)
            ? [cur, ...items]
            : items,
        );
      } catch {
        setExOptions([]);
      } finally {
        setExLoading(false);
      }
    }, 300);
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
    } catch (err) {
      setError(extractApiError(err, "Erro ao adicionar exercício."));
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
    } catch (err) {
      setError(extractApiError(err, "Erro ao remover exercício."));
    } finally {
      setLoadingRemove(false);
    }
  };

  const handleDuplicar = async () => {
    setLoadingDuplicar(true);
    try {
      const res = await treinadorApi.duplicarFicha(treinoId);
      router.push(`/treinador/treinos/${res.data.treinoId}`);
    } catch (err) {
      setError(extractApiError(err, "Erro ao duplicar ficha."));
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
    } catch (err) {
      setError(extractApiError(err, "Erro ao verificar vínculos da ficha."));
      return;
    }
    setSelectedAluno(null);
    setVincularOpen(true);
    if (alunos.length === 0) {
      try {
        const res = await treinadorApi.listAlunos({ status: "Ativo", tamanhoPagina: MAX_PAGE_SIZE });
        setAlunos(res.data.items);
      } catch (err) {
        setError(extractApiError(err, "Erro ao carregar alunos ativos."));
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
    } catch (err) {
      setError(extractApiError(err, "Erro ao vincular ficha."));
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
    } catch (err) {
      setError(extractApiError(err, "Erro ao atualizar ficha."));
    } finally {
      setLoadingEdit(false);
    }
  };

  const openEditEx = (ex: TreinoExercicioResponse) => {
    setEditExTarget(ex);
    setEditSeriesRows(ex.series.map((s) => ({
      quantidade: String(s.quantidade),
      repeticoesMin: String(s.repeticoesMin),
      repeticoesMax: s.repeticoesMax != null ? String(s.repeticoesMax) : "",
      descricao: s.descricao ?? "",
      carga: s.carga != null ? String(s.carga) : "",
      descanso: s.descanso != null ? String(s.descanso) : "",
    })));
  };

  const updateEditSerieRow = (idx: number, field: keyof SerieRow, value: string) => {
    setEditSeriesRows((prev) => prev.map((r, i) => i === idx ? { ...r, [field]: value } : r));
  };

  const addEditSerieRow = () => setEditSeriesRows((prev) => [...prev, { ...SERIE_VAZIA, descricao: "" }]);
  const removeEditSerieRow = (idx: number) => setEditSeriesRows((prev) => prev.filter((_, i) => i !== idx));

  const handleEditarExercicio = async () => {
    if (!editExTarget) return;
    setLoadingEditEx(true);
    try {
      const series: SerieConfigData[] = editSeriesRows.map((r) => ({
        quantidade: Number(r.quantidade) || 1,
        repeticoesMin: Number(r.repeticoesMin) || 1,
        repeticoesMax: r.repeticoesMax ? Number(r.repeticoesMax) : null,
        descricao: r.descricao.trim() || null,
        carga: r.carga ? Number(r.carga) : null,
        descanso: r.descanso ? Number(r.descanso) : null,
      }));
      await treinadorApi.editarExercicioTreino(treinoId, editExTarget.treinoExercicioId, { series });
      setSuccess("Exercício atualizado.");
      setEditExTarget(null);
      load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao editar exercício."));
    } finally {
      setLoadingEditEx(false);
    }
  };

  const openObs = (ex: TreinoExercicioResponse) => {
    setObsTarget(ex);
    setObsText(ex.observacao ?? "");
  };

  const handleSalvarObs = async () => {
    if (!obsTarget) return;
    setLoadingObs(true);
    try {
      await treinadorApi.atualizarObservacaoExercicio(treinoId, obsTarget.treinoExercicioId, obsText.trim() || null);
      setSuccess("Observação salva.");
      setObsTarget(null);
      load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao salvar observação."));
    } finally {
      setLoadingObs(false);
    }
  };

  const handleExcluir = async () => {
    setLoadingDelete(true);
    try {
      await treinadorApi.excluirFicha(treinoId);
      router.push("/treinador/treinos");
    } catch (err) {
      setError(extractApiError(err, "Erro ao excluir ficha. Fichas com execuções registradas não podem ser excluídas."));
      setDeleteOpen(false);
      setLoadingDelete(false);
    }
  };

  const canAdd = !!selectedEx && seriesRows.every(
    (r) => Number(r.quantidade) >= 1 && Number(r.repeticoesMin) >= 1
  );

  if (loading) return <LoadingSpinner />;
  if (!ficha) {
    return (
      <DetalheErro
        mensagem={error || "Não foi possível carregar a ficha."}
        onRetry={load}
        onVoltar={() => router.push("/treinador/treinos")}
      />
    );
  }

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 3 }}>
        <IconButton onClick={() => router.push("/treinador/treinos")} aria-label="Voltar">
          <ArrowBackIcon />
        </IconButton>
        <Box sx={{ flex: 1 }}>
          <Typography variant="h5">{ficha.nome}</Typography>
          <Chip label={OBJETIVO_LABEL[ficha.objetivo] ?? ficha.objetivo} size="small" sx={{ mt: 0.5 }} />
        </Box>
        <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }}>
          <Button variant="outlined" size="small" startIcon={<ContentCopyIcon />} disabled={loadingDuplicar} onClick={handleDuplicar}
            sx={{ minWidth: { xs: 36, sm: "auto" }, px: { xs: 1, sm: 1.5 }, "& .MuiButton-startIcon": { mr: { xs: 0, sm: 0.5 } } }}
            aria-label="Duplicar">
            <Box component="span" sx={{ display: { xs: "none", sm: "inline" } }}>Duplicar</Box>
          </Button>
          <Button variant="outlined" size="small" startIcon={<FileDownloadIcon />}
            onClick={() => void exportarFichaParaExcel({ nome: ficha.nome, objetivo: ficha.objetivo, exercicios: ficha.exercicios })}
            sx={{ minWidth: { xs: 36, sm: "auto" }, px: { xs: 1, sm: 1.5 }, "& .MuiButton-startIcon": { mr: { xs: 0, sm: 0.5 } } }}
            aria-label="Exportar">
            <Box component="span" sx={{ display: { xs: "none", sm: "inline" } }}>Exportar</Box>
          </Button>
          <Button variant="outlined" size="small" startIcon={<LinkIcon />} onClick={openVincular}
            sx={{ minWidth: { xs: 36, sm: "auto" }, px: { xs: 1, sm: 1.5 }, "& .MuiButton-startIcon": { mr: { xs: 0, sm: 0.5 } } }}
            aria-label="Vincular aluno">
            <Box component="span" sx={{ display: { xs: "none", sm: "inline" } }}>Vincular aluno</Box>
          </Button>
          <Button variant="outlined" size="small" startIcon={<EditIcon />} onClick={openEdit}
            sx={{ minWidth: { xs: 36, sm: "auto" }, px: { xs: 1, sm: 1.5 }, "& .MuiButton-startIcon": { mr: { xs: 0, sm: 0.5 } } }}
            aria-label="Editar">
            <Box component="span" sx={{ display: { xs: "none", sm: "inline" } }}>Editar</Box>
          </Button>
          <Button variant="outlined" size="small" color="error" startIcon={<DeleteIcon />} onClick={() => setDeleteOpen(true)}
            sx={{ minWidth: { xs: 36, sm: "auto" }, px: { xs: 1, sm: 1.5 }, "& .MuiButton-startIcon": { mr: { xs: 0, sm: 0.5 } } }}
            aria-label="Excluir">
            <Box component="span" sx={{ display: { xs: "none", sm: "inline" } }}>Excluir</Box>
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
              if (i === 2) return ex.observacao ? (
                <Typography variant="body2" sx={{ fontSize: 12, color: "text.secondary", maxWidth: 200, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                  {ex.observacao}
                </Typography>
              ) : (
                <Typography variant="body2" sx={{ fontSize: 12, color: "text.disabled" }}>—</Typography>
              );
              return (
                <Box sx={{ display: "flex", gap: 0.5, justifyContent: "flex-end" }}>
                  <Tooltip title="Editar séries">
                    <IconButton size="small" onClick={() => openEditEx(ex)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title={ex.observacao ? "Editar observação" : "Adicionar observação"}>
                    <IconButton size="small" color={ex.observacao ? "primary" : "default"} onClick={() => openObs(ex)}>
                      <NoteAltIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Remover exercício">
                    <IconButton size="small" color="error" onClick={() => setRemoveEx(ex)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </Box>
              );
            }}
          />
        )}
      </Card>

      {/* Dialog: adicionar exercício */}
      <Dialog open={addOpen} onClose={() => setAddOpen(false)} maxWidth="md" fullWidth fullScreen={isMobile} slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Adicionar exercício</DialogTitle>
        <DialogContent>
          <Stack spacing={2.5} sx={{ pt: 1 }}>
            <Autocomplete
              options={exOptions}
              getOptionLabel={(ex) => `${ex.nome}${ex.grupoMuscular ? ` — ${GRUPO_MUSCULAR_LABEL[ex.grupoMuscular] ?? ex.grupoMuscular}` : ""}`}
              isOptionEqualToValue={(o, v) => o.exercicioId === v.exercicioId}
              filterOptions={(x) => x}
              value={selectedEx}
              onChange={(_, v) => {
                setSelectedEx(v);
                selectedExRef.current = v;
              }}
              onInputChange={handleExInputChange}
              loading={exLoading}
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
                        sx={{ width: { xs: 80, sm: 90 } }}
                        slotProps={{ htmlInput: { min: 1 } }}
                      />
                      <TextField
                        label="Reps mín."
                        type="number"
                        value={row.repeticoesMin}
                        onChange={(e) => updateSerieRow(idx, "repeticoesMin", e.target.value)}
                        size="small"
                        sx={{ width: { xs: 80, sm: 90 } }}
                        slotProps={{ htmlInput: { min: 1 } }}
                      />
                      <TextField
                        label="Reps máx."
                        type="number"
                        value={row.repeticoesMax}
                        onChange={(e) => updateSerieRow(idx, "repeticoesMax", e.target.value)}
                        size="small"
                        sx={{ width: { xs: 80, sm: 90 } }}
                        slotProps={{ htmlInput: { min: 1 } }}
                      />
                      <TextField
                        label="Descrição"
                        value={row.descricao}
                        onChange={(e) => updateSerieRow(idx, "descricao", e.target.value)}
                        size="small"
                        sx={{ width: { xs: "100%", sm: "auto" }, flex: { xs: "none", sm: 1 }, minWidth: { xs: 0, sm: 120 } }}
                        placeholder="Ex.: Aquecimento"
                        slotProps={{ htmlInput: { maxLength: 100 } }}
                      />
                      <TextField
                        label="Carga (kg)"
                        type="number"
                        value={row.carga}
                        onChange={(e) => updateSerieRow(idx, "carga", e.target.value)}
                        size="small"
                        sx={{ width: { xs: 80, sm: 90 } }}
                        slotProps={{ htmlInput: { min: 0, step: 0.5 } }}
                      />
                      <TextField
                        label="Descanso (s)"
                        type="number"
                        value={row.descanso}
                        onChange={(e) => updateSerieRow(idx, "descanso", e.target.value)}
                        size="small"
                        sx={{ width: { xs: 80, sm: 100 } }}
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
      <Dialog open={editOpen} onClose={() => setEditOpen(false)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
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
              {OBJETIVOS.map((o) => <MenuItem key={o} value={o}>{OBJETIVO_LABEL[o]}</MenuItem>)}
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

      {/* Dialog: editar séries do exercício */}
      <Dialog open={!!editExTarget} onClose={() => setEditExTarget(null)} maxWidth="md" fullWidth fullScreen={isMobile} slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Editar séries — {editExTarget?.nomeExercicio}</DialogTitle>
        <DialogContent>
          <Box sx={{ pt: 1 }}>
            <Stack spacing={1.5} divider={<Divider />}>
              {editSeriesRows.map((row, idx) => (
                <Box key={idx}>
                  <Stack direction="row" sx={{ alignItems: "center", gap: 1, flexWrap: "wrap" }}>
                    <TextField
                      label="Qtd séries"
                      type="number"
                      value={row.quantidade}
                      onChange={(e) => updateEditSerieRow(idx, "quantidade", e.target.value)}
                      size="small"
                      sx={{ width: { xs: 80, sm: 90 } }}
                      slotProps={{ htmlInput: { min: 1 } }}
                    />
                    <TextField
                      label="Reps mín."
                      type="number"
                      value={row.repeticoesMin}
                      onChange={(e) => updateEditSerieRow(idx, "repeticoesMin", e.target.value)}
                      size="small"
                      sx={{ width: { xs: 80, sm: 90 } }}
                      slotProps={{ htmlInput: { min: 1 } }}
                    />
                    <TextField
                      label="Reps máx."
                      type="number"
                      value={row.repeticoesMax}
                      onChange={(e) => updateEditSerieRow(idx, "repeticoesMax", e.target.value)}
                      size="small"
                      sx={{ width: { xs: 80, sm: 90 } }}
                      slotProps={{ htmlInput: { min: 1 } }}
                    />
                    <TextField
                      label="Descrição"
                      value={row.descricao}
                      onChange={(e) => updateEditSerieRow(idx, "descricao", e.target.value)}
                      size="small"
                      sx={{ width: { xs: "100%", sm: "auto" }, flex: { xs: "none", sm: 1 }, minWidth: { xs: 0, sm: 120 } }}
                      placeholder="Ex.: Aquecimento"
                      slotProps={{ htmlInput: { maxLength: 100 } }}
                    />
                    <TextField
                      label="Carga (kg)"
                      type="number"
                      value={row.carga}
                      onChange={(e) => updateEditSerieRow(idx, "carga", e.target.value)}
                      size="small"
                      sx={{ width: { xs: 80, sm: 90 } }}
                      slotProps={{ htmlInput: { min: 0, step: 0.5 } }}
                    />
                    <TextField
                      label="Descanso (s)"
                      type="number"
                      value={row.descanso}
                      onChange={(e) => updateEditSerieRow(idx, "descanso", e.target.value)}
                      size="small"
                      sx={{ width: { xs: 80, sm: 100 } }}
                      slotProps={{ htmlInput: { min: 0 } }}
                    />
                    {editSeriesRows.length > 1 && (
                      <Tooltip title="Remover grupo">
                        <IconButton size="small" color="error" onClick={() => removeEditSerieRow(idx)}>
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    )}
                  </Stack>
                </Box>
              ))}
            </Stack>
            <Button startIcon={<AddIcon />} size="small" onClick={addEditSerieRow} sx={{ mt: 1.5 }}>
              Adicionar grupo de séries
            </Button>
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditExTarget(null)}>Cancelar</Button>
          <Button
            variant="contained"
            disabled={loadingEditEx || editSeriesRows.every((r) => !(Number(r.quantidade) >= 1 && Number(r.repeticoesMin) >= 1))}
            onClick={handleEditarExercicio}
          >
            Salvar
          </Button>
        </DialogActions>
      </Dialog>

      {/* Dialog: observação do exercício */}
      <Dialog open={!!obsTarget} onClose={() => setObsTarget(null)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>
          {obsTarget?.observacao ? "Editar observação" : "Adicionar observação"}
        </DialogTitle>
        <DialogContent>
          <TextField
            label="Observação"
            value={obsText}
            onChange={(e) => setObsText(e.target.value)}
            size="small"
            fullWidth
            multiline
            minRows={3}
            maxRows={6}
            autoFocus
            placeholder="Ex.: Manter cotovelo alinhado, foco na contração..."
            slotProps={{ htmlInput: { maxLength: 500 } }}
            helperText={`${obsText.length}/500`}
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setObsTarget(null)}>Cancelar</Button>
          {obsTarget?.observacao && (
            <Button color="error" disabled={loadingObs} onClick={() => { setObsText(""); }}>
              Limpar
            </Button>
          )}
          <Button variant="contained" disabled={loadingObs} onClick={handleSalvarObs}>
            Salvar
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={vincularOpen} onClose={() => setVincularOpen(false)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
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
