"use client";
import { useCallback, useState } from "react";
import {
  Box, Typography, Button, Dialog, DialogTitle, DialogContent, DialogActions,
  Stack, TextField, Select, MenuItem, FormControl, InputLabel, IconButton, Tooltip, Autocomplete,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import { useRouter } from "next/navigation";
import AlertBanner from "@/components/ui/AlertBanner";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import DataList from "@/components/ui/DataList";
import type { Column } from "@/components/ui/ResponsiveTable";
import { treinadorApi } from "@/lib/api/treinador";
import type { TreinoResponse, ObjetivoTreino, DificuldadeTreino, AlunoResponse } from "@/types";
import { OBJETIVOS, OBJETIVOS_FILTRO, DIFICULDADES } from "@/lib/constants/labels";
import { usePaginatedList } from "@/hooks/usePaginatedList";
import { useCRUDDialog } from "@/hooks/useCRUDDialog";

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

  const {
    createOpen, openCreate, closeCreate, creating, setCreating,
    editTarget, openEdit, closeEdit, editing, setEditing,
    deleteTarget, openDelete, closeDelete, deleting, setDeleting,
  } = useCRUDDialog<TreinoResponse>();

  const [nome, setNome] = useState("");
  const [objetivo, setObjetivo] = useState<ObjetivoTreino>("Hipertrofia");
  const [dificuldade, setDificuldade] = useState<DificuldadeTreino>("Iniciante");
  const [dataInicio, setDataInicio] = useState("");
  const [dataFim, setDataFim] = useState("");
  const [alunos, setAlunos] = useState<AlunoResponse[]>([]);
  const [selectedAluno, setSelectedAluno] = useState<AlunoResponse | null>(null);
  const [editNome, setEditNome] = useState("");
  const [editObjetivo, setEditObjetivo] = useState<ObjetivoTreino>("Hipertrofia");
  const [editDificuldade, setEditDificuldade] = useState<DificuldadeTreino>("Iniciante");
  const [editDataInicio, setEditDataInicio] = useState("");
  const [editDataFim, setEditDataFim] = useState("");

  // filtros
  const [filtroNome, setFiltroNome] = useState("");
  const [filtroObjetivo, setFiltroObjetivo] = useState("");
  const [ordenarPor, setOrdenarPor] = useState("nome");

  const fetcher = useCallback(
    (p: number, ps: number) =>
      treinadorApi.listFichas({
        pagina: p + 1,
        tamanhoPagina: ps,
        nome: filtroNome || undefined,
        objetivo: filtroObjetivo || undefined,
        ordenarPor,
      }).then((r) => r.data),
    [filtroNome, filtroObjetivo, ordenarPor]
  );
  const { items: fichas, total, page, pageSize, loading, error, setPage, setPageSize, setError, reload } =
    usePaginatedList<TreinoResponse>({ fetcher, errorMessage: "Erro ao carregar fichas." });

  const resetForm = () => { setNome(""); setObjetivo("Hipertrofia"); setDificuldade("Iniciante"); setDataInicio(""); setDataFim(""); setSelectedAluno(null); };

  const openDialog = async () => {
    openCreate();
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
    if (!nome.trim()) return;
    setCreating(true);
    try {
      const res = await treinadorApi.criarFicha({
        alunoId: selectedAluno?.alunoId ?? null,
        nome: nome.trim(),
        objetivo,
        dificuldade,
        dataInicio: dataInicio || null,
        dataFim: dataFim || null,
      });
      closeCreate();
      resetForm();
      router.push(`/treinador/treinos/${res.data.treinoId}`);
    } catch {
      setError("Erro ao criar ficha.");
      setCreating(false);
    }
  };

  const handleOpenEdit = (f: TreinoResponse) => {
    openEdit(f);
    setEditNome(f.nome);
    setEditObjetivo(f.objetivo);
    setEditDificuldade(f.dificuldade);
    setEditDataInicio(f.dataInicio ?? "");
    setEditDataFim(f.dataFim ?? "");
  };

  const handleEditar = async () => {
    if (!editTarget || !editNome.trim()) return;
    setEditing(true);
    try {
      await treinadorApi.atualizarFicha(editTarget.treinoId, {
        nome: editNome.trim(),
        objetivo: editObjetivo,
        dificuldade: editDificuldade,
        dataInicio: editDataInicio || null,
        dataFim: editDataFim || null,
        limparDataInicio: !editDataInicio,
        limparDataFim: !editDataFim,
      });
      closeEdit();
      reload();
    } catch {
      setError("Erro ao atualizar ficha.");
    } finally {
      setEditing(false);
    }
  };

  const handleExcluir = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await treinadorApi.excluirFicha(deleteTarget.treinoId);
      closeDelete();
      reload();
    } catch {
      setError("Erro ao excluir ficha. Fichas com execuções registradas não podem ser excluídas.");
      closeDelete();
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
      <DataList
        loading={loading}
        items={fichas}
        emptyMessage="Nenhuma ficha cadastrada. Crie a primeira para começar a prescrever treinos."
        emptyActionLabel="Criar primeira ficha"
        onEmptyAction={openDialog}
        columns={COLUMNS}
        rowKey={(f) => f.treinoId}
        onRowClick={(f) => router.push(`/treinador/treinos/${f.treinoId}`)}
        pagination={{ count: total, page, rowsPerPage: pageSize, onPageChange: setPage, onRowsPerPageChange: setPageSize }}
        renderCell={(f, i) => {
          if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{f.nome}</Typography>;
          if (i === 1) return OBJETIVOS_FILTRO.find((o) => o.value === f.objetivo)?.label ?? f.objetivo;
          if (i === 2) return (
            <Typography variant="body2" color={f.nomeAluno ? "text.primary" : "text.disabled"}>
              {f.nomeAluno ?? "—"}
            </Typography>
          );
          if (i === 3) return f.exercicios.length;
          if (i === 4) return (
            <Typography variant="caption">{new Date(f.createdAt).toLocaleDateString("pt-BR")}</Typography>
          );
          return (
            <Box sx={{ display: "flex", gap: 0.5, justifyContent: "flex-end" }}>
              <Tooltip title="Abrir ficha">
                <IconButton size="small" onClick={(e) => { e.stopPropagation(); router.push(`/treinador/treinos/${f.treinoId}`); }}>
                  <OpenInNewIcon fontSize="small" />
                </IconButton>
              </Tooltip>
              <Tooltip title="Editar">
                <IconButton size="small" onClick={(e) => { e.stopPropagation(); handleOpenEdit(f); }}>
                  <EditIcon fontSize="small" />
                </IconButton>
              </Tooltip>
              <Tooltip title="Excluir">
                <IconButton size="small" color="error" onClick={(e) => { e.stopPropagation(); openDelete(f); }}>
                  <DeleteIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            </Box>
          );
        }}
      />

      {/* Dialog: criar */}
      <Dialog open={createOpen} onClose={() => { closeCreate(); resetForm(); }} maxWidth="xs" fullWidth>
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
                  label="Aluno (opcional)"
                  size="small"
                  helperText="Deixe em branco para criar sem vincular. Vincule depois pela página do aluno."
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
                  <MenuItem key={o} value={o}>{OBJETIVOS_FILTRO.find((f) => f.value === o)?.label ?? o}</MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl size="small" fullWidth>
              <InputLabel>Dificuldade</InputLabel>
              <Select
                value={dificuldade}
                label="Dificuldade"
                onChange={(e) => setDificuldade(e.target.value as DificuldadeTreino)}
              >
                {DIFICULDADES.map((d) => (
                  <MenuItem key={d.value} value={d.value}>{d.label}</MenuItem>
                ))}
              </Select>
            </FormControl>
            <Stack direction="row" spacing={1.5}>
              <TextField
                label="Início (opcional)"
                type="date"
                value={dataInicio}
                onChange={(e) => setDataInicio(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ inputLabel: { shrink: true } }}
              />
              <TextField
                label="Validade (opcional)"
                type="date"
                value={dataFim}
                onChange={(e) => setDataFim(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ inputLabel: { shrink: true } }}
              />
            </Stack>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { closeCreate(); resetForm(); }}>Cancelar</Button>
          <Button variant="contained" disabled={!nome.trim() || creating} onClick={handleCriar}>
            Criar e abrir
          </Button>
        </DialogActions>
      </Dialog>

      {/* Dialog: editar */}
      <Dialog open={!!editTarget} onClose={closeEdit} maxWidth="xs" fullWidth>
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
                  <MenuItem key={o} value={o}>{OBJETIVOS_FILTRO.find((f) => f.value === o)?.label ?? o}</MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl size="small" fullWidth>
              <InputLabel>Dificuldade</InputLabel>
              <Select
                value={editDificuldade}
                label="Dificuldade"
                onChange={(e) => setEditDificuldade(e.target.value as DificuldadeTreino)}
              >
                {DIFICULDADES.map((d) => (
                  <MenuItem key={d.value} value={d.value}>{d.label}</MenuItem>
                ))}
              </Select>
            </FormControl>
            <Stack direction="row" spacing={1.5}>
              <TextField
                label="Início (opcional)"
                type="date"
                value={editDataInicio}
                onChange={(e) => setEditDataInicio(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ inputLabel: { shrink: true } }}
              />
              <TextField
                label="Validade (opcional)"
                type="date"
                value={editDataFim}
                onChange={(e) => setEditDataFim(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ inputLabel: { shrink: true } }}
              />
            </Stack>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={closeEdit}>Cancelar</Button>
          <Button variant="contained" disabled={!editNome.trim() || editing} onClick={handleEditar}>
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
        onClose={closeDelete}
      />
    </Box>
  );
}
