"use client";
import { useCallback, useEffect, useState } from "react";
import {
  Box, Typography, Card, Button, Dialog, DialogTitle, DialogContent, DialogActions,
  Stack, TextField, Select, MenuItem, FormControl, InputLabel, IconButton, Tooltip, Autocomplete,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import { useRouter } from "next/navigation";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import { treinadorApi } from "@/lib/api/treinador";
import type { TreinoResponse, ObjetivoTreino, AlunoResponse } from "@/types";

const OBJETIVOS: ObjetivoTreino[] = [
  "Hipertrofia", "Emagrecimento", "Resistencia", "Forca", "Flexibilidade", "Condicionamento",
];

const OBJETIVOS_FILTRO: { value: string; label: string }[] = [
  { value: "Hipertrofia", label: "Hipertrofia" },
  { value: "Emagrecimento", label: "Emagrecimento" },
  { value: "Resistencia", label: "Resistência" },
  { value: "Forca", label: "Força" },
  { value: "Flexibilidade", label: "Flexibilidade" },
  { value: "Condicionamento", label: "Condicionamento" },
];

const COLUMNS: Column[] = [
  { label: "Nome" },
  { label: "Objetivo" },
  { label: "Aluno" },
  { label: "Exercícios" },
  { label: "Criado em" },
  { label: "Ações", align: "right" },
];

export default function TreinosTreinadorPage() {
  const router = useRouter();
  const [fichas, setFichas] = useState<TreinoResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // criar
  const [open, setOpen] = useState(false);
  const [nome, setNome] = useState("");
  const [objetivo, setObjetivo] = useState<ObjetivoTreino>("Hipertrofia");
  const [saving, setSaving] = useState(false);
  const [alunos, setAlunos] = useState<AlunoResponse[]>([]);
  const [selectedAluno, setSelectedAluno] = useState<AlunoResponse | null>(null);

  // editar
  const [editTarget, setEditTarget] = useState<TreinoResponse | null>(null);
  const [editNome, setEditNome] = useState("");
  const [editObjetivo, setEditObjetivo] = useState<ObjetivoTreino>("Hipertrofia");
  const [editSaving, setEditSaving] = useState(false);

  // excluir
  const [deleteTarget, setDeleteTarget] = useState<TreinoResponse | null>(null);
  const [deleting, setDeleting] = useState(false);

  // filtros
  const [filtroNome, setFiltroNome] = useState("");
  const [filtroObjetivo, setFiltroObjetivo] = useState("");
  const [ordenarPor, setOrdenarPor] = useState("nome");

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await treinadorApi.listFichas({
        pagina: page + 1,
        tamanhoPagina: pageSize,
        nome: filtroNome || undefined,
        objetivo: filtroObjetivo || undefined,
        ordenarPor,
      });
      setFichas(res.data.items);
      setTotal(res.data.total);
    } catch {
      setError("Erro ao carregar fichas.");
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, filtroNome, filtroObjetivo, ordenarPor]);

  useEffect(() => { load(); }, [load]);

  const resetForm = () => { setNome(""); setObjetivo("Hipertrofia"); setSelectedAluno(null); };

  const openDialog = async () => {
    setOpen(true);
    if (alunos.length === 0) {
      try {
        const res = await treinadorApi.listAlunos({ status: "Ativo", tamanhoPagina: 200 });
        setAlunos(res.data.items);
      } catch {
        setError("Erro ao carregar alunos ativos.");
      }
    }
  };

  const handleCriar = async () => {
    if (!nome.trim() || !selectedAluno) return;
    setSaving(true);
    try {
      const res = await treinadorApi.criarFicha({ alunoId: selectedAluno.alunoId, nome: nome.trim(), objetivo });
      setOpen(false);
      resetForm();
      router.push(`/treinador/treinos/${res.data.treinoId}`);
    } catch {
      setError("Erro ao criar ficha.");
      setSaving(false);
    }
  };

  const openEdit = (f: TreinoResponse) => {
    setEditTarget(f);
    setEditNome(f.nome);
    setEditObjetivo(f.objetivo);
  };

  const handleEditar = async () => {
    if (!editTarget || !editNome.trim()) return;
    setEditSaving(true);
    try {
      await treinadorApi.atualizarFicha(editTarget.treinoId, { nome: editNome.trim(), objetivo: editObjetivo });
      setEditTarget(null);
      load();
    } catch {
      setError("Erro ao atualizar ficha.");
    } finally {
      setEditSaving(false);
    }
  };

  const handleExcluir = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await treinadorApi.excluirFicha(deleteTarget.treinoId);
      setDeleteTarget(null);
      load();
    } catch {
      setError("Erro ao excluir ficha. Fichas com execuções registradas não podem ser excluídas.");
      setDeleteTarget(null);
    } finally {
      setDeleting(false);
    }
  };

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700 }}>Fichas de Treino</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openDialog}>
          Nova ficha
        </Button>
      </Box>

      <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ mb: 2 }}>
        <TextField
          label="Buscar por nome"
          value={filtroNome}
          onChange={(e) => { setFiltroNome(e.target.value); setPage(0); }}
          size="small"
          sx={{ minWidth: 200 }}
        />
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel>Objetivo</InputLabel>
          <Select
            value={filtroObjetivo}
            label="Objetivo"
            onChange={(e) => { setFiltroObjetivo(e.target.value); setPage(0); }}
          >
            <MenuItem value="">Todos</MenuItem>
            {OBJETIVOS_FILTRO.map((o) => (
              <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel>Ordenar por</InputLabel>
          <Select
            value={ordenarPor}
            label="Ordenar por"
            onChange={(e) => { setOrdenarPor(e.target.value); setPage(0); }}
          >
            <MenuItem value="nome">Nome</MenuItem>
            <MenuItem value="objetivo">Objetivo</MenuItem>
            <MenuItem value="nomeAluno">Aluno</MenuItem>
            <MenuItem value="createdAt">Data de criação</MenuItem>
          </Select>
        </FormControl>
      </Stack>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <Card variant="outlined">
        {loading ? (
          <LoadingSpinner />
        ) : fichas.length === 0 ? (
          <EmptyState
            message="Nenhuma ficha cadastrada. Crie a primeira para começar a prescrever treinos."
            actionLabel="Criar primeira ficha"
            onAction={openDialog}
          />
        ) : (
          <ResponsiveTable
            columns={COLUMNS}
            rows={fichas}
            rowKey={(f) => f.treinoId}
            onRowClick={(f) => router.push(`/treinador/treinos/${f.treinoId}`)}
            pagination={{
              count: total,
              page,
              rowsPerPage: pageSize,
              onPageChange: setPage,
              onRowsPerPageChange: (size) => { setPageSize(size); setPage(0); },
            }}
            renderCell={(f, i) => {
              if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{f.nome}</Typography>;
              if (i === 1) return f.objetivo;
              if (i === 2) return (
                <Typography variant="body2" color={f.nomeAluno ? "text.primary" : "text.disabled"}>
                  {f.nomeAluno ?? "—"}
                </Typography>
              );
              if (i === 3) return f.exercicios.length;
              if (i === 4) return (
                <Typography variant="caption">
                  {new Date(f.createdAt).toLocaleDateString("pt-BR")}
                </Typography>
              );
              return (
                <Box sx={{ display: "flex", gap: 0.5, justifyContent: "flex-end" }}>
                  <Tooltip title="Abrir ficha">
                    <IconButton
                      size="small"
                      onClick={(e) => { e.stopPropagation(); router.push(`/treinador/treinos/${f.treinoId}`); }}
                    >
                      <OpenInNewIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Editar">
                    <IconButton
                      size="small"
                      onClick={(e) => { e.stopPropagation(); openEdit(f); }}
                    >
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Excluir">
                    <IconButton
                      size="small"
                      color="error"
                      onClick={(e) => { e.stopPropagation(); setDeleteTarget(f); }}
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </Box>
              );
            }}
          />
        )}
      </Card>

      {/* Dialog: criar */}
      <Dialog open={open} onClose={() => { setOpen(false); resetForm(); }} maxWidth="xs" fullWidth>
        <DialogTitle>Nova ficha de treino</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <Autocomplete
              options={alunos}
              getOptionLabel={(a) => a.nome}
              value={selectedAluno}
              onChange={(_, v) => setSelectedAluno(v)}
              noOptionsText="Nenhum aluno ativo na carteira"
              renderInput={(params) => (
                <TextField
                  {...params}
                  label="Aluno"
                  size="small"
                  required
                  helperText="A ficha será automaticamente vinculada ao aluno selecionado."
                />
              )}
            />
            <TextField
              label="Nome"
              value={nome}
              onChange={(e) => setNome(e.target.value)}
              size="small"
              fullWidth
              required
              autoFocus
            />
            <FormControl size="small" fullWidth>
              <InputLabel>Objetivo</InputLabel>
              <Select
                value={objetivo}
                label="Objetivo"
                onChange={(e) => setObjetivo(e.target.value as ObjetivoTreino)}
              >
                {OBJETIVOS.map((o) => (
                  <MenuItem key={o} value={o}>{o}</MenuItem>
                ))}
              </Select>
            </FormControl>
            {alunos.length === 0 && (
              <Typography variant="caption" color="text.secondary">
                Para criar fichas vinculadas, é necessário ter ao menos um aluno ativo na sua carteira.
              </Typography>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { setOpen(false); resetForm(); }}>Cancelar</Button>
          <Button variant="contained" disabled={!nome.trim() || !selectedAluno || saving} onClick={handleCriar}>
            Criar e abrir
          </Button>
        </DialogActions>
      </Dialog>

      {/* Dialog: editar */}
      <Dialog open={!!editTarget} onClose={() => setEditTarget(null)} maxWidth="xs" fullWidth>
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
            <FormControl size="small" fullWidth>
              <InputLabel>Objetivo</InputLabel>
              <Select
                value={editObjetivo}
                label="Objetivo"
                onChange={(e) => setEditObjetivo(e.target.value as ObjetivoTreino)}
              >
                {OBJETIVOS.map((o) => (
                  <MenuItem key={o} value={o}>{o}</MenuItem>
                ))}
              </Select>
            </FormControl>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditTarget(null)}>Cancelar</Button>
          <Button variant="contained" disabled={!editNome.trim() || editSaving} onClick={handleEditar}>
            Salvar
          </Button>
        </DialogActions>
      </Dialog>

      {/* Dialog: confirmar exclusão */}
      <ConfirmDialog
        open={!!deleteTarget}
        title="Excluir ficha"
        description={`Excluir "${deleteTarget?.nome}"? Fichas com execuções registradas não podem ser excluídas.`}
        confirmLabel="Excluir"
        destructive
        loading={deleting}
        onConfirm={handleExcluir}
        onClose={() => setDeleteTarget(null)}
      />
    </Box>
  );
}
