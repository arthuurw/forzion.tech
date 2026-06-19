"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import {
  Box, Typography, Tabs, Tab, Card, Button, Dialog, DialogTitle, DialogContent, DialogActions,
  Stack, TextField, Select, MenuItem, FormControl, InputLabel, IconButton, Tooltip,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import { treinadorApi } from "@/lib/api/treinador";
import { parseYouTubeId } from "@/lib/utils/youtube";
import type { ExercicioResponse, GrupoMuscularResponse } from "@/types";
import { GRUPO_MUSCULAR_LABEL } from "@/lib/constants/labels";
import { MAX_PAGE_SIZE } from "@/lib/constants/pagination";
import { extractApiError } from "@/lib/api/extractApiError";

const COLS_MEUS: Column[] = [
  { label: "Nome" },
  { label: "Grupo muscular" },
  { label: "Descrição" },
  { label: "Ações", align: "right" },
];

const COLS_GLOBAIS: Column[] = [
  { label: "Nome" },
  { label: "Grupo muscular" },
  { label: "Descrição" },
  { label: "Ações", align: "right" },
];

type TabState = {
  page: number;
  filtroNome: string;
  filtroGrupo: string;
  ordenarPor: "nome" | "grupoMuscular";
};

const INITIAL_TAB_STATE: TabState = {
  page: 0,
  filtroNome: "",
  filtroGrupo: "",
  ordenarPor: "nome",
};

export default function ExerciciosTreinadorPage() {
  const [tab, setTab] = useState(0);
  const [tabState, setTabState] = useState<[TabState, TabState]>([
    { ...INITIAL_TAB_STATE },
    { ...INITIAL_TAB_STATE },
  ]);
  const [pageSize, setPageSize] = useState(10);

  const { page, filtroNome, filtroGrupo, ordenarPor } = tabState[tab];

  const patchTab = useCallback((patch: Partial<TabState>, targetTab?: number) => {
    setTabState((s) => {
      const idx = targetTab ?? tab;
      const next = [...s] as [TabState, TabState];
      next[idx] = { ...next[idx], ...patch };
      return next;
    });
  }, [tab]);

  const loadIdRef = useRef(0);

  const [exercicios, setExercicios] = useState<ExercicioResponse[]>([]);
  const [grupos, setGrupos] = useState<GrupoMuscularResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [open, setOpen] = useState(false);
  const [nome, setNome] = useState("");
  const [descricao, setDescricao] = useState("");
  const [comoExecutar, setComoExecutar] = useState("");
  const [videoUrl, setVideoUrl] = useState("");
  const [grupoMuscular, setGrupoMuscular] = useState("");
  const [saving, setSaving] = useState(false);

  const [editEx, setEditEx] = useState<ExercicioResponse | null>(null);
  const [editNome, setEditNome] = useState("");
  const [editGrupo, setEditGrupo] = useState("");
  const [editDescricao, setEditDescricao] = useState("");
  const [editComoExecutar, setEditComoExecutar] = useState("");
  const [editVideoUrl, setEditVideoUrl] = useState("");
  const [savingEdit, setSavingEdit] = useState(false);

  const [confirmExcluir, setConfirmExcluir] = useState<ExercicioResponse | null>(null);
  const [loadingExcluir, setLoadingExcluir] = useState(false);

  const [copiando, setCopiando] = useState<string | null>(null);
  const [confirmCopiarDuplicado, setConfirmCopiarDuplicado] = useState<ExercicioResponse | null>(null);
  const [meusNomes, setMeusNomes] = useState<Set<string>>(new Set());

  const isGlobal = tab === 1;

  const load = useCallback(async () => {
    const callId = ++loadIdRef.current;
    setLoading(true);
    setError("");
    try {
      const res = await treinadorApi.listExercicios({
        global: isGlobal,
        pagina: page + 1,
        tamanhoPagina: pageSize,
        nome: filtroNome || undefined,
        grupoMuscularId: filtroGrupo || undefined,
        ordenarPor,
      });
      if (callId !== loadIdRef.current) return;
      setExercicios(res.data.items);
      setTotal(res.data.total);
    } catch {
      if (callId !== loadIdRef.current) return;
      setError("Erro ao carregar exercícios.");
    } finally {
      if (callId !== loadIdRef.current) return;
      setLoading(false);
    }
  }, [isGlobal, page, pageSize, filtroNome, filtroGrupo, ordenarPor]);

  const loadGrupos = useCallback(async () => {
    try {
      const res = await treinadorApi.listGruposMusculares();
      setGrupos(res.data);
      if (res.data.length > 0) {
        setGrupoMuscular(res.data[0].id);
        setEditGrupo(res.data[0].id);
      }
    } catch {
      // grupos musculares indisponíveis — formulário fica sem seleção de grupo
    }
  }, []);

  const loadMeusNomes = useCallback(async () => {
    try {
      const res = await treinadorApi.listExercicios({ global: false, pagina: 1, tamanhoPagina: MAX_PAGE_SIZE });
      setMeusNomes(new Set(res.data.items.map((e: ExercicioResponse) => e.nome.toLowerCase())));
    } catch { /* validação de duplicatas desabilitada — meusNomes permanece vazio */ }
  }, []);

  useEffect(() => {
    load();
    loadGrupos();
    loadMeusNomes();
  }, [load, loadGrupos, loadMeusNomes]);

  const handleTabChange = (_: unknown, v: number) => {
    setTab(v);
  };

  const resetForm = () => { setNome(""); setDescricao(""); setComoExecutar(""); setVideoUrl(""); setGrupoMuscular(grupos.length > 0 ? grupos[0].id : ""); };

  const videoUrlInvalido = videoUrl.trim() !== "" && parseYouTubeId(videoUrl) === null;
  const editVideoUrlInvalido = editVideoUrl.trim() !== "" && parseYouTubeId(editVideoUrl) === null;

  const handleCriar = async () => {
    if (!nome.trim() || videoUrlInvalido) return;
    setSaving(true);
    try {
      await treinadorApi.criarExercicio({
        nome: nome.trim(),
        descricao: descricao.trim() || null,
        comoExecutar: comoExecutar.trim() || null,
        videoUrl: videoUrl.trim() || null,
        grupoMuscularId: grupoMuscular,
      });
      setSuccess(`Exercício "${nome.trim()}" criado.`);
      setOpen(false);
      resetForm();
      load();
      loadMeusNomes();
    } catch (err) {
      setError(extractApiError(err, "Erro ao criar exercício."));
    } finally {
      setSaving(false);
    }
  };

  const openEdit = (ex: ExercicioResponse) => {
    setEditEx(ex);
    setEditNome(ex.nome);
    setEditGrupo(ex.grupoMuscularId);
    setEditDescricao(ex.descricao ?? "");
    setEditComoExecutar(ex.comoExecutar ?? "");
    setEditVideoUrl(ex.videoId ?? "");
  };

  const handleEditar = async () => {
    if (!editEx || editVideoUrlInvalido) return;
    setSavingEdit(true);
    try {
      await treinadorApi.atualizarExercicio(editEx.exercicioId, {
        nome: editNome.trim() || undefined,
        grupoMuscularId: editGrupo || undefined,
        descricao: editDescricao.trim() || null,
        comoExecutar: editComoExecutar.trim(),
        videoUrl: editVideoUrl.trim(),
      });
      setSuccess(`"${editNome}" atualizado.`);
      setEditEx(null);
      load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao atualizar exercício."));
    } finally {
      setSavingEdit(false);
    }
  };

  const handleExcluir = async () => {
    if (!confirmExcluir) return;
    setLoadingExcluir(true);
    try {
      await treinadorApi.excluirExercicio(confirmExcluir.exercicioId);
      setSuccess(`"${confirmExcluir.nome}" excluído.`);
      setConfirmExcluir(null);
      load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao excluir exercício. Pode estar em uso em fichas."));
    } finally {
      setLoadingExcluir(false);
    }
  };

  const executarCopia = async (ex: ExercicioResponse) => {
    setCopiando(ex.exercicioId);
    try {
      await treinadorApi.copiarExercicioGlobal(ex.exercicioId);
      setSuccess(`"${ex.nome}" copiado para sua biblioteca.`);
      setConfirmCopiarDuplicado(null);
      setMeusNomes((prev) => new Set([...prev, ex.nome.toLowerCase()]));
    } catch (err) {
      setError(extractApiError(err, "Erro ao copiar exercício."));
    } finally {
      setCopiando(null);
    }
  };

  const handleCopiar = async (ex: ExercicioResponse) => {
    if (meusNomes.has(ex.nome.toLowerCase())) {
      setConfirmCopiarDuplicado(ex);
      return;
    }
    await executarCopia(ex);
  };

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
        <Typography variant="h5" sx={{ fontWeight: 700 }}>Exercícios</Typography>
        {!isGlobal && (
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>
            Novo exercício
          </Button>
        )}
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Tabs value={tab} onChange={handleTabChange} sx={{ mb: 2 }}>
        <Tab label="Meus exercícios" />
        <Tab label="Globais" />
      </Tabs>

      <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ mb: 2 }}>
        <TextField
          label="Buscar por nome"
          value={filtroNome}
          onChange={(e) => patchTab({ filtroNome: e.target.value, page: 0 })}
          size="small"
          sx={{ minWidth: 200 }}
        />
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel>Grupo muscular</InputLabel>
          <Select
            value={filtroGrupo}
            label="Grupo muscular"
            onChange={(e) => patchTab({ filtroGrupo: e.target.value, page: 0 })}
          >
            <MenuItem value="">Todos</MenuItem>
            {grupos.map((g) => <MenuItem key={g.id} value={g.id}>{g.nome}</MenuItem>)}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel>Ordenar por</InputLabel>
          <Select
            value={ordenarPor}
            label="Ordenar por"
            onChange={(e) => patchTab({ ordenarPor: e.target.value as "nome" | "grupoMuscular", page: 0 })}
          >
            <MenuItem value="nome">Nome</MenuItem>
            <MenuItem value="grupoMuscular">Grupo muscular</MenuItem>
          </Select>
        </FormControl>
      </Stack>

      <Card variant="outlined">
        {loading ? (
          <LoadingSpinner />
        ) : exercicios.length === 0 ? (
          <EmptyState
            message={isGlobal ? "Nenhum exercício global disponível." : "Nenhum exercício criado ainda."}
            actionLabel={!isGlobal ? "Criar exercício" : undefined}
            onAction={!isGlobal ? () => setOpen(true) : undefined}
          />
        ) : (
          <ResponsiveTable
            columns={isGlobal ? COLS_GLOBAIS : COLS_MEUS}
            rows={exercicios}
            rowKey={(ex) => ex.exercicioId}
            pagination={{
              count: total,
              page,
              rowsPerPage: pageSize,
              onPageChange: (p) => patchTab({ page: p }),
              onRowsPerPageChange: (size) => { setPageSize(size); patchTab({ page: 0 }); },
            }}
            renderCell={(ex, i) => {
              if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{ex.nome}</Typography>;
              if (i === 1) return ex.grupoMuscular ? (GRUPO_MUSCULAR_LABEL[ex.grupoMuscular] ?? ex.grupoMuscular) : "—";
              if (i === 2) return (
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ display: "block", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", maxWidth: { xs: "100%", md: 280 } }}
                >
                  {ex.descricao ?? "—"}
                </Typography>
              );
              if (isGlobal) return (
                <Tooltip title="Copiar para minha biblioteca">
                  <span>
                    <IconButton size="small" disabled={copiando === ex.exercicioId} onClick={() => handleCopiar(ex)}>
                      <ContentCopyIcon fontSize="small" />
                    </IconButton>
                  </span>
                </Tooltip>
              );
              return (
                <>
                  <Tooltip title="Editar">
                    <IconButton size="small" onClick={() => openEdit(ex)}><EditIcon fontSize="small" /></IconButton>
                  </Tooltip>
                  <Tooltip title="Excluir">
                    <IconButton size="small" color="error" onClick={() => setConfirmExcluir(ex)}><DeleteIcon fontSize="small" /></IconButton>
                  </Tooltip>
                </>
              );
            }}
          />
        )}
      </Card>

      {/* Criar */}
      <Dialog open={open} onClose={() => { setOpen(false); resetForm(); }} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Novo exercício</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} size="small" fullWidth required autoFocus />
            <FormControl size="small" fullWidth>
              <InputLabel>Grupo muscular</InputLabel>
              <Select value={grupoMuscular} label="Grupo muscular" onChange={(e) => setGrupoMuscular(e.target.value)}>
                {grupos.map((g) => <MenuItem key={g.id} value={g.id}>{g.nome}</MenuItem>)}
              </Select>
            </FormControl>
            <TextField label="Descrição (opcional)" value={descricao} onChange={(e) => setDescricao(e.target.value)} size="small" fullWidth multiline rows={2} />
            <TextField label="Como executar (opcional)" value={comoExecutar} onChange={(e) => setComoExecutar(e.target.value)} size="small" fullWidth multiline rows={3} slotProps={{ htmlInput: { maxLength: 2000 } }} />
            <TextField
              label="Link do vídeo (YouTube, opcional)"
              value={videoUrl}
              onChange={(e) => setVideoUrl(e.target.value)}
              size="small"
              fullWidth
              error={videoUrlInvalido}
              helperText={videoUrlInvalido ? "Informe um link ou ID de vídeo do YouTube válido." : " "}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { setOpen(false); resetForm(); }}>Cancelar</Button>
          <Button variant="contained" disabled={!nome.trim() || saving || videoUrlInvalido} onClick={handleCriar}>Criar</Button>
        </DialogActions>
      </Dialog>

      {/* Editar */}
      <Dialog open={!!editEx} onClose={() => setEditEx(null)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Editar — {editEx?.nome}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField label="Nome" value={editNome} onChange={(e) => setEditNome(e.target.value)} size="small" fullWidth />
            <FormControl size="small" fullWidth>
              <InputLabel>Grupo muscular</InputLabel>
              <Select value={editGrupo} label="Grupo muscular" onChange={(e) => setEditGrupo(e.target.value)}>
                {grupos.map((g) => <MenuItem key={g.id} value={g.id}>{g.nome}</MenuItem>)}
              </Select>
            </FormControl>
            <TextField label="Descrição" value={editDescricao} onChange={(e) => setEditDescricao(e.target.value)} size="small" fullWidth multiline rows={2} />
            <TextField label="Como executar" value={editComoExecutar} onChange={(e) => setEditComoExecutar(e.target.value)} size="small" fullWidth multiline rows={3} slotProps={{ htmlInput: { maxLength: 2000 } }} />
            <TextField
              label="Link do vídeo (YouTube)"
              value={editVideoUrl}
              onChange={(e) => setEditVideoUrl(e.target.value)}
              size="small"
              fullWidth
              error={editVideoUrlInvalido}
              helperText={editVideoUrlInvalido ? "Informe um link ou ID de vídeo do YouTube válido." : " "}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditEx(null)}>Cancelar</Button>
          <Button variant="contained" disabled={savingEdit || editVideoUrlInvalido} onClick={handleEditar}>Salvar</Button>
        </DialogActions>
      </Dialog>

      {/* Copiar duplicado */}
      <ConfirmDialog
        open={!!confirmCopiarDuplicado}
        title="Exercício já existe na sua biblioteca"
        description={`Você já possui um exercício chamado "${confirmCopiarDuplicado?.nome}". Deseja copiar mesmo assim?`}
        confirmLabel="Copiar mesmo assim"
        loading={copiando === confirmCopiarDuplicado?.exercicioId}
        onConfirm={() => confirmCopiarDuplicado && executarCopia(confirmCopiarDuplicado)}
        onClose={() => setConfirmCopiarDuplicado(null)}
      />

      {/* Excluir */}
      <ConfirmDialog
        open={!!confirmExcluir}
        title="Excluir exercício"
        description={`Excluir "${confirmExcluir?.nome}"? Exercícios em uso em fichas não podem ser excluídos.`}
        confirmLabel="Excluir"
        destructive
        loading={loadingExcluir}
        onConfirm={handleExcluir}
        onClose={() => setConfirmExcluir(null)}
      />
    </Box>
  );
}
