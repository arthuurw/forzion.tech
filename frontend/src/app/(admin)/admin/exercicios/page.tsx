"use client";
import { useCallback, useEffect, useState } from "react";
import {
  Box, Typography, Button, Dialog, DialogTitle, DialogContent, DialogActions,
  Stack, TextField, Select, MenuItem, FormControl, InputLabel, IconButton, Tooltip,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import AlertBanner from "@/components/ui/AlertBanner";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import DataList from "@/components/ui/DataList";
import type { Column } from "@/components/ui/ResponsiveTable";
import { adminApi } from "@/lib/api/admin";
import type { ExercicioResponse, GrupoMuscularResponse } from "@/types";
import { usePaginatedList } from "@/hooks/usePaginatedList";
import { useCRUDDialog } from "@/hooks/useCRUDDialog";
import { extractApiError } from "@/lib/api/extractApiError";
import { GRUPO_MUSCULAR_LABEL } from "@/lib/constants/labels";

const COLUMNS: Column[] = [
  { label: "Nome" },
  { label: "Grupo muscular" },
  { label: "Descrição" },
  { label: "Ações", align: "right" },
];

export default function ExerciciosAdminPage() {
  const [grupos, setGrupos] = useState<GrupoMuscularResponse[]>([]);

  const [filtroNome, setFiltroNome] = useState("");
  const [filtroGrupo, setFiltroGrupo] = useState("");
  const [ordenarPor, setOrdenarPor] = useState<"nome" | "grupoMuscular">("nome");

  const {
    createOpen: criarOpen, openCreate: openCriar, closeCreate: closeCriar, creating: saving, setCreating: setSaving,
    editTarget: editEx, openEdit, closeEdit, editing: savingEdit, setEditing: setSavingEdit,
    deleteTarget: confirmExcluir, openDelete: openExcluir, closeDelete: closeExcluir,
    deleting: loadingExcluir, setDeleting: setLoadingExcluir,
  } = useCRUDDialog<ExercicioResponse>();

  const [nome, setNome] = useState("");
  const [grupoMuscular, setGrupoMuscular] = useState("");
  const [descricao, setDescricao] = useState("");
  const [editNome, setEditNome] = useState("");
  const [editGrupo, setEditGrupo] = useState("");
  const [editDescricao, setEditDescricao] = useState("");

  const fetcher = useCallback(
    (p: number, ps: number) =>
      adminApi.listExerciciosGlobais({
        pagina: p + 1,
        tamanhoPagina: ps,
        nome: filtroNome || undefined,
        grupoMuscularId: filtroGrupo || undefined,
        ordenarPor,
      }).then((r) => r.data),
    [filtroNome, filtroGrupo, ordenarPor]
  );
  const { items: exercicios, total, page, pageSize, loading, error, success, setPage, setPageSize, setError, setSuccess, reload } =
    usePaginatedList<ExercicioResponse>({ fetcher, errorMessage: "Erro ao carregar exercícios." });

  const loadGrupos = useCallback(async () => {
    try {
      const res = await adminApi.listGruposMusculares();
      setGrupos(res.data);
      if (res.data.length > 0) {
        setGrupoMuscular(res.data[0].id);
        setEditGrupo(res.data[0].id);
      }
    } catch (err) {
      // sem grupos o formulário fica sem seleção de grupo: avisa em vez de falhar mudo
      setError(extractApiError(err, "Não foi possível carregar os grupos musculares. O cadastro de exercícios fica indisponível."));
    }
  }, [setError]);

  useEffect(() => { loadGrupos(); }, [loadGrupos]);

  const resetForm = () => {
    setNome("");
    if (grupos.length > 0) setGrupoMuscular(grupos[0].id);
    setDescricao("");
  };

  const handleCriar = async () => {
    if (!nome.trim()) return;
    setSaving(true);
    try {
      await adminApi.criarExercicioGlobal({ nome: nome.trim(), grupoMuscularId: grupoMuscular, descricao: descricao.trim() || null });
      setSuccess(`"${nome.trim()}" adicionado.`);
      closeCriar();
      resetForm();
      reload();
    } catch {
      setError("Erro ao criar exercício.");
    } finally {
      setSaving(false);
    }
  };

  const handleOpenEdit = (ex: ExercicioResponse) => {
    openEdit(ex);
    setEditNome(ex.nome);
    setEditGrupo(ex.grupoMuscularId);
    setEditDescricao(ex.descricao ?? "");
  };

  const handleEditar = async () => {
    if (!editEx) return;
    setSavingEdit(true);
    try {
      await adminApi.atualizarExercicioGlobal(editEx.exercicioId, {
        nome: editNome.trim() || undefined,
        grupoMuscularId: editGrupo || undefined,
        descricao: editDescricao.trim() || null,
      });
      setSuccess(`"${editNome}" atualizado.`);
      closeEdit();
      reload();
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
      closeExcluir();
      reload();
    } catch {
      setError("Erro ao excluir exercício. Pode estar em uso em fichas.");
    } finally {
      setLoadingExcluir(false);
    }
  };

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3, flexWrap: "wrap", gap: 2 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>Biblioteca Global</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            Exercícios disponíveis para todos os treinadores copiarem.
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openCriar}>
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
            {grupos.map((g) => <MenuItem key={g.id} value={g.id}>{g.nome}</MenuItem>)}
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

      <DataList
        loading={loading}
        items={exercicios}
        emptyMessage="Nenhum exercício encontrado."
        emptyActionLabel="Adicionar exercício"
        onEmptyAction={openCriar}
        columns={COLUMNS}
        rowKey={(ex) => ex.exercicioId}
        pagination={{ count: total, page, rowsPerPage: pageSize, onPageChange: setPage, onRowsPerPageChange: setPageSize }}
        renderCell={(ex, i) => {
          if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{ex.nome}</Typography>;
          if (i === 1) return ex.grupoMuscular ? (GRUPO_MUSCULAR_LABEL[ex.grupoMuscular] ?? ex.grupoMuscular) : "—";
          if (i === 2) return (
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", maxWidth: { xs: "100%", md: 320 } }}>
              {ex.descricao ?? "—"}
            </Typography>
          );
          return (
            <>
              <Tooltip title="Editar">
                <IconButton size="small" aria-label="Editar exercício" onClick={() => handleOpenEdit(ex)}><EditIcon fontSize="small" /></IconButton>
              </Tooltip>
              <Tooltip title="Excluir">
                <IconButton size="small" color="error" aria-label="Excluir exercício" onClick={() => openExcluir(ex)}><DeleteIcon fontSize="small" /></IconButton>
              </Tooltip>
            </>
          );
        }}
      />

      {/* Criar */}
      <Dialog open={criarOpen} onClose={() => { closeCriar(); resetForm(); }} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Novo exercício global</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} size="small" fullWidth required autoFocus />
            <FormControl size="small" fullWidth required>
              <InputLabel>Grupo muscular</InputLabel>
              <Select value={grupoMuscular} label="Grupo muscular" onChange={(e) => setGrupoMuscular(e.target.value)}>
                {grupos.map((g) => <MenuItem key={g.id} value={g.id}>{g.nome}</MenuItem>)}
              </Select>
            </FormControl>
            <TextField label="Descrição (opcional)" value={descricao} onChange={(e) => setDescricao(e.target.value)} size="small" fullWidth multiline rows={3} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { closeCriar(); resetForm(); }}>Cancelar</Button>
          <Button variant="contained" disabled={!nome.trim() || saving} onClick={handleCriar}>Adicionar</Button>
        </DialogActions>
      </Dialog>

      {/* Editar */}
      <Dialog open={!!editEx} onClose={closeEdit} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Editar — {editEx?.nome}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField label="Nome" value={editNome} onChange={(e) => setEditNome(e.target.value)} size="small" fullWidth />
            <FormControl size="small" fullWidth>
              <InputLabel>Grupo muscular</InputLabel>
              <Select value={editGrupo} label="Grupo muscular" onChange={(e) => setEditGrupo(e.target.value as string)}>
                {grupos.map((g) => <MenuItem key={g.id} value={g.id}>{g.nome}</MenuItem>)}
              </Select>
            </FormControl>
            <TextField label="Descrição" value={editDescricao} onChange={(e) => setEditDescricao(e.target.value)} size="small" fullWidth multiline rows={3} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={closeEdit}>Cancelar</Button>
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
        onClose={closeExcluir}
      />
    </Box>
  );
}
