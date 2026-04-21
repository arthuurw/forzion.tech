"use client";
import { useCallback, useEffect, useState } from "react";
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
import type { ExercicioResponse, GrupoMuscularResponse } from "@/types";

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

export default function ExerciciosTreinadorPage() {
  const [tab, setTab] = useState(0);
  const [exercicios, setExercicios] = useState<ExercicioResponse[]>([]);
  const [grupos, setGrupos] = useState<GrupoMuscularResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [filtroNome, setFiltroNome] = useState("");
  const [filtroGrupo, setFiltroGrupo] = useState("");
  const [ordenarPor, setOrdenarPor] = useState<"nome" | "grupoMuscular">("nome");

  const [open, setOpen] = useState(false);
  const [nome, setNome] = useState("");
  const [descricao, setDescricao] = useState("");
  const [grupoMuscular, setGrupoMuscular] = useState("");
  const [saving, setSaving] = useState(false);

  const [editEx, setEditEx] = useState<ExercicioResponse | null>(null);
  const [editNome, setEditNome] = useState("");
  const [editGrupo, setEditGrupo] = useState("");
  const [editDescricao, setEditDescricao] = useState("");
  const [savingEdit, setSavingEdit] = useState(false);

  const [confirmExcluir, setConfirmExcluir] = useState<ExercicioResponse | null>(null);
  const [loadingExcluir, setLoadingExcluir] = useState(false);

  const [copiando, setCopiando] = useState<string | null>(null);

  const isGlobal = tab === 1;

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await treinadorApi.listExercicios({
        global: isGlobal,
        pagina: page + 1,
        tamanhoPagina: pageSize,
        nome: filtroNome || undefined,
        grupoMuscular: filtroGrupo || undefined,
        ordenarPor,
      });
      setExercicios(res.data.items);
      setTotal(res.data.total);
    } catch {
      setError("Erro ao carregar exercícios.");
    } finally {
      setLoading(false);
    }
  }, [isGlobal, page, pageSize, filtroNome, filtroGrupo, ordenarPor]);

  const loadGrupos = useCallback(async () => {
    try {
      const res = await treinadorApi.listGruposMusculares();
      setGrupos(res.data);
      if (res.data.length > 0) {
        setGrupoMuscular(res.data[0].nome);
        setEditGrupo(res.data[0].nome);
      }
    } catch {
      console.error("Erro ao carregar grupos musculares.");
    }
  }, []);

  useEffect(() => { 
    load(); 
    loadGrupos();
  }, [load, loadGrupos]);

  const handleTabChange = (_: unknown, v: number) => {
    setTab(v);
    setPage(0);
    setFiltroNome("");
    setFiltroGrupo("");
    setOrdenarPor("nome");
  };

  const resetForm = () => { setNome(""); setDescricao(""); setGrupoMuscular("Peito"); };

  const handleCriar = async () => {
    if (!nome.trim()) return;
    setSaving(true);
    try {
      await treinadorApi.criarExercicio({
        nome: nome.trim(),
        descricao: descricao.trim() || null,
        grupoMuscular: grupoMuscular || null,
      });
      setSuccess(`Exercício "${nome.trim()}" criado.`);
      setOpen(false);
      resetForm();
      load();
    } catch {
      setError("Erro ao criar exercício.");
    } finally {
      setSaving(false);
    }
  };

  const openEdit = (ex: ExercicioResponse) => {
    setEditEx(ex);
    setEditNome(ex.nome);
    setEditGrupo(ex.grupoMuscular ?? (grupos.length > 0 ? grupos[0].nome : ""));
    setEditDescricao(ex.descricao ?? "");
  };

  const handleEditar = async () => {
    if (!editEx) return;
    setSavingEdit(true);
    try {
      await treinadorApi.atualizarExercicio(editEx.exercicioId, {
        nome: editNome.trim() || undefined,
        grupoMuscular: editGrupo || undefined,
        descricao: editDescricao.trim() || null,
      });
      setSuccess(`"${editNome}" atualizado.`);
      setEditEx(null);
      load();
    } catch {
      setError("Erro ao atualizar exercício.");
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
    } catch {
      setError("Erro ao excluir exercício. Pode estar em uso em fichas.");
    } finally {
      setLoadingExcluir(false);
    }
  };

  const handleCopiar = async (ex: ExercicioResponse) => {
    setCopiando(ex.exercicioId);
    try {
      await treinadorApi.copiarExercicioGlobal(ex.exercicioId);
      setSuccess(`"${ex.nome}" copiado para sua biblioteca.`);
    } catch {
      setError("Erro ao copiar exercício.");
    } finally {
      setCopiando(null);
    }
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
          onChange={(e) => { setFiltroNome(e.target.value); setPage(0); }}
          size="small"
          sx={{ minWidth: 200 }}
        />
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel>Grupo muscular</InputLabel>
          <Select
            value={filtroGrupo}
            label="Grupo muscular"
            onChange={(e) => { setFiltroGrupo(e.target.value); setPage(0); }}
          >
            <MenuItem value="">Todos</MenuItem>
            {grupos.map((g) => <MenuItem key={g.id} value={g.nome}>{g.nome}</MenuItem>)}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel>Ordenar por</InputLabel>
          <Select
            value={ordenarPor}
            label="Ordenar por"
            onChange={(e) => { setOrdenarPor(e.target.value as "nome" | "grupoMuscular"); setPage(0); }}
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
              onPageChange: setPage,
              onRowsPerPageChange: (size) => { setPageSize(size); setPage(0); },
            }}
            renderCell={(ex, i) => {
              if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{ex.nome}</Typography>;
              if (i === 1) return ex.grupoMuscular ?? "—";
              if (i === 2) return (
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ display: "block", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", maxWidth: { md: 280 } }}
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
      <Dialog open={open} onClose={() => { setOpen(false); resetForm(); }} maxWidth="xs" fullWidth>
        <DialogTitle>Novo exercício</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} size="small" fullWidth required autoFocus />
            <FormControl size="small" fullWidth>
              <InputLabel>Grupo muscular</InputLabel>
              <Select value={grupoMuscular} label="Grupo muscular" onChange={(e) => setGrupoMuscular(e.target.value as GrupoMuscular)}>
                {GRUPOS.map((g) => <MenuItem key={g} value={g}>{g}</MenuItem>)}
              </Select>
            </FormControl>
            <TextField label="Descrição (opcional)" value={descricao} onChange={(e) => setDescricao(e.target.value)} size="small" fullWidth multiline rows={2} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { setOpen(false); resetForm(); }}>Cancelar</Button>
          <Button variant="contained" disabled={!nome.trim() || saving} onClick={handleCriar}>Criar</Button>
        </DialogActions>
      </Dialog>

      {/* Editar */}
      <Dialog open={!!editEx} onClose={() => setEditEx(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Editar — {editEx?.nome}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField label="Nome" value={editNome} onChange={(e) => setEditNome(e.target.value)} size="small" fullWidth />
            <FormControl size="small" fullWidth>
              <InputLabel>Grupo muscular</InputLabel>
              <Select value={editGrupo} label="Grupo muscular" onChange={(e) => setEditGrupo(e.target.value as GrupoMuscular)}>
                {GRUPOS.map((g) => <MenuItem key={g} value={g}>{g}</MenuItem>)}
              </Select>
            </FormControl>
            <TextField label="Descrição" value={editDescricao} onChange={(e) => setEditDescricao(e.target.value)} size="small" fullWidth multiline rows={2} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditEx(null)}>Cancelar</Button>
          <Button variant="contained" disabled={savingEdit} onClick={handleEditar}>Salvar</Button>
        </DialogActions>
      </Dialog>

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
);
}
