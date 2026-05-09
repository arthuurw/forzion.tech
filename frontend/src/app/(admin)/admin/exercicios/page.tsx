"use client";
import { useCallback, useEffect, useState } from "react";
import {
  Box, Typography, Card, Button, Dialog, DialogTitle, DialogContent, DialogActions,
  Stack, TextField, Select, MenuItem, FormControl, InputLabel, IconButton, Tooltip,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import { adminApi, type GrupoMuscularEnum } from "@/lib/api/admin";
import type { ExercicioResponse, GrupoMuscularResponse } from "@/types";

const COLUMNS: Column[] = [
  { label: "Nome" },
  { label: "Grupo muscular" },
  { label: "Descrição" },
  { label: "Ações", align: "right" },
];

export default function ExerciciosAdminPage() {
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

  const [criarOpen, setCriarOpen] = useState(false);
  const [nome, setNome] = useState("");
  const [grupoMuscular, setGrupoMuscular] = useState("");
  const [descricao, setDescricao] = useState("");
  const [saving, setSaving] = useState(false);

  const [editEx, setEditEx] = useState<ExercicioResponse | null>(null);
  const [editNome, setEditNome] = useState("");
  const [editGrupo, setEditGrupo] = useState("");
  const [editDescricao, setEditDescricao] = useState("");
  const [savingEdit, setSavingEdit] = useState(false);

  const [confirmExcluir, setConfirmExcluir] = useState<ExercicioResponse | null>(null);
  const [loadingExcluir, setLoadingExcluir] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await adminApi.listExerciciosGlobais({
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
  }, [page, pageSize, filtroNome, filtroGrupo, ordenarPor]);

  const loadGrupos = useCallback(async () => {
    try {
      const res = await adminApi.listGruposMusculares();
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

  const resetForm = () => { 
    setNome(""); 
    if (grupos.length > 0) setGrupoMuscular(grupos[0].nome);
    setDescricao(""); 
  };

  const handleCriar = async () => {
    if (!nome.trim()) return;
    setSaving(true);
    try {
      await adminApi.criarExercicioGlobal({ nome: nome.trim(), grupoMuscular: grupoMuscular as GrupoMuscularEnum, descricao: descricao.trim() || null });
      setSuccess(`"${nome.trim()}" adicionado.`);
      setCriarOpen(false);
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
    setEditGrupo((ex.grupoMuscular as GrupoMuscularEnum) ?? "Peito");
    setEditDescricao(ex.descricao ?? "");
  };

  const handleEditar = async () => {
    if (!editEx) return;
    setSavingEdit(true);
    try {
      await adminApi.atualizarExercicioGlobal(editEx.exercicioId, {
        nome: editNome.trim() || undefined,
        grupoMuscular: (editGrupo as GrupoMuscularEnum) || undefined,
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
      await adminApi.excluirExercicioGlobal(confirmExcluir.exercicioId);
      setSuccess(`"${confirmExcluir.nome}" excluído.`);
      setConfirmExcluir(null);
      load();
    } catch {
      setError("Erro ao excluir exercício. Pode estar em uso em fichas.");
    } finally {
      setLoadingExcluir(false);
    }
  };

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>Biblioteca Global</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            Exercícios disponíveis para todos os treinadores copiarem.
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCriarOpen(true)}>
          Novo exercício
        </Button>
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

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
          <Select value={filtroGrupo} label="Grupo muscular" onChange={(e) => { setFiltroGrupo(e.target.value as string); setPage(0); }}>
            <MenuItem value="">Todos</MenuItem>
            {grupos.map((g) => <MenuItem key={g.id} value={g.nome}>{g.nome}</MenuItem>)}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel>Ordenar por</InputLabel>
          <Select value={ordenarPor} label="Ordenar por" onChange={(e) => { setOrdenarPor(e.target.value as "nome" | "grupoMuscular"); setPage(0); }}>
            <MenuItem value="nome">Nome</MenuItem>
            <MenuItem value="grupoMuscular">Grupo muscular</MenuItem>
          </Select>
        </FormControl>
      </Stack>

      <Card variant="outlined">
        {loading ? <LoadingSpinner /> : exercicios.length === 0 ? (
          <EmptyState message="Nenhum exercício encontrado." actionLabel="Adicionar exercício" onAction={() => setCriarOpen(true)} />
        ) : (
          <ResponsiveTable
            columns={COLUMNS}
            rows={exercicios}
            rowKey={(ex) => ex.exercicioId}
            pagination={{ count: total, page, rowsPerPage: pageSize, onPageChange: setPage, onRowsPerPageChange: (s) => { setPageSize(s); setPage(0); } }}
            renderCell={(ex, i) => {
              if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{ex.nome}</Typography>;
              if (i === 1) return ex.grupoMuscular ?? "—";
              if (i === 2) return (
                <Typography variant="caption" color="text.secondary" sx={{ display: "block", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", maxWidth: { md: 320 } }}>
                  {ex.descricao ?? "—"}
                </Typography>
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
      <Dialog open={criarOpen} onClose={() => { setCriarOpen(false); resetForm(); }} maxWidth="xs" fullWidth>
        <DialogTitle>Novo exercício global</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} size="small" fullWidth required autoFocus />
            <FormControl size="small" fullWidth required>
              <InputLabel>Grupo muscular</InputLabel>
              <Select value={grupoMuscular} label="Grupo muscular" onChange={(e) => setGrupoMuscular(e.target.value)}>
                {grupos.map((g) => <MenuItem key={g.id} value={g.nome}>{g.nome}</MenuItem>)}
              </Select>
            </FormControl>
            <TextField label="Descrição (opcional)" value={descricao} onChange={(e) => setDescricao(e.target.value)} size="small" fullWidth multiline rows={3} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { setCriarOpen(false); resetForm(); }}>Cancelar</Button>
          <Button variant="contained" disabled={!nome.trim() || saving} onClick={handleCriar}>Adicionar</Button>
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
              <Select value={editGrupo} label="Grupo muscular" onChange={(e) => setEditGrupo(e.target.value as string)}>
                {grupos.map((g) => <MenuItem key={g.id} value={g.nome}>{g.nome}</MenuItem>)}
              </Select>
            </FormControl>
            <TextField label="Descrição" value={editDescricao} onChange={(e) => setEditDescricao(e.target.value)} size="small" fullWidth multiline rows={3} />
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
        description={`Excluir "${confirmExcluir?.nome}" da biblioteca global? Exercícios em uso em fichas não podem ser excluídos.`}
        confirmLabel="Excluir"
        destructive
        loading={loadingExcluir}
        onConfirm={handleExcluir}
        onClose={() => setConfirmExcluir(null)}
      />
    </Box>
  );
}
