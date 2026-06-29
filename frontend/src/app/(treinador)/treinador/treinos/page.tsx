"use client";
import { useCallback, useState } from "react";
import {
  Box, Typography, Button, Dialog, DialogTitle, DialogContent, DialogActions,
  Stack, TextField, Select, MenuItem, FormControl, InputLabel, FormHelperText, IconButton, Tooltip,
} from "@mui/material";
import { DatePicker } from "@mui/x-date-pickers/DatePicker";
import dayjs from "dayjs";
import AddIcon from "@mui/icons-material/Add";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import { useRouter } from "next/navigation";
import AlertBanner from "@/components/ui/AlertBanner";
import PageHeader from "@/components/ui/PageHeader";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import DataList from "@/components/ui/DataList";
import type { Column } from "@/components/ui/ResponsiveTable";
import { treinadorApi } from "@/lib/api/treinador";
import { extractApiError } from "@/lib/api/extractApiError";
import type { TreinoResponse, ObjetivoTreino, DificuldadeTreino, AlunoResponse } from "@/types";
import { OBJETIVOS, OBJETIVOS_FILTRO, DIFICULDADES } from "@/lib/constants/labels";
import { usePaginatedList } from "@/hooks/usePaginatedList";
import { useCRUDDialog } from "@/hooks/useCRUDDialog";
import { MAX_PAGE_SIZE } from "@/lib/constants/pagination";

// DatePicker trabalha com Dayjs; o back espera ISO YYYY-MM-DD ("" = sem data / limpar)
const toIsoDate = (d: dayjs.Dayjs | null) => (d?.isValid() ? d.format("YYYY-MM-DD") : "");
// DatePicker renderiza PickersInputBase (não .MuiOutlinedInput-input), então o override de
// fonte do tema não alcança — igualamos à fonte dos demais campos (0.875rem no desktop).
const DATE_FIELD_SLOTS = {
  textField: {
    size: "small" as const,
    fullWidth: true,
    // shrink fixo: campo vazio mantém o label acima (igual aos demais, que já têm valor/foco)
    InputLabelProps: { shrink: true },
    sx: { "& .MuiPickersInputBase-root": { fontSize: { xs: "1rem", sm: "0.875rem" } } },
  },
};

const COLUMNS: Column[] = [
  { label: "Nome" },
  { label: "Objetivo" },
  { label: "Dificuldade" },
  { label: "Aluno" },
  { label: "Exercícios", align: "center" },
  { label: "Início", align: "center" },
  { label: "Validade", align: "center" },
  { label: "Criado em", align: "center" },
  // align center p/ o header ficar sobre o meio dos 3 botões; mobileRole explícito mantém o
  // tratamento de "ações" no card mobile, que antes vinha de align:"right"
  { label: "Ações", align: "center", mobileRole: "actions" },
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
  const [selectedAlunoId, setSelectedAlunoId] = useState("");
  const [editNome, setEditNome] = useState("");
  const [editObjetivo, setEditObjetivo] = useState<ObjetivoTreino>("Hipertrofia");
  const [editDificuldade, setEditDificuldade] = useState<DificuldadeTreino>("Iniciante");
  const [editDataInicio, setEditDataInicio] = useState("");
  const [editDataFim, setEditDataFim] = useState("");

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
    usePaginatedList<TreinoResponse>({ fetcher, errorMessage: "Erro ao carregar fichas.", initialPageSize: 25 });

  const resetForm = () => { setNome(""); setObjetivo("Hipertrofia"); setDificuldade("Iniciante"); setDataInicio(""); setDataFim(""); setSelectedAlunoId(""); };

  const openDialog = async () => {
    openCreate();
    if (alunos.length === 0) {
      try {
        const res = await treinadorApi.listAlunos({ status: "Ativo", tamanhoPagina: MAX_PAGE_SIZE });
        setAlunos(res.data.items);
      } catch (err) {
        setError(extractApiError(err, "Erro ao carregar alunos ativos."));
      }
    }
  };

  const handleCriar = async () => {
    if (!nome.trim()) return;
    setCreating(true);
    try {
      const res = await treinadorApi.criarFicha({
        alunoId: selectedAlunoId || null,
        nome: nome.trim(),
        objetivo,
        dificuldade,
        dataInicio: dataInicio || null,
        dataFim: dataFim || null,
      });
      closeCreate();
      resetForm();
      router.push(`/treinador/treinos/${res.data.treinoId}`);
    } catch (err) {
      setError(extractApiError(err, "Erro ao criar ficha."));
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
    } catch (err) {
      setError(extractApiError(err, "Erro ao atualizar ficha."));
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
    } catch (err) {
      setError(extractApiError(err, "Erro ao excluir ficha. Fichas com execuções registradas não podem ser excluídas."));
      closeDelete();
    } finally {
      setDeleting(false);
    }
  };

  return (
    <Box>
      <PageHeader
        title="Fichas de Treino"
        action={
          <Button variant="contained" startIcon={<AddIcon />} onClick={openDialog}>
            Nova ficha
          </Button>
        }
      />

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
            <MenuItem value="dificuldade">Dificuldade</MenuItem>
            <MenuItem value="nomeAluno">Aluno</MenuItem>
            <MenuItem value="exercicios">Nº de exercícios</MenuItem>
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
          if (i === 2) {
            const d = DIFICULDADES.find((x) => x.value === f.dificuldade);
            return <Typography variant="body2" sx={{ color: d?.color, fontWeight: 700 }}>{d?.label ?? f.dificuldade}</Typography>;
          }
          if (i === 3) return (
            <Typography variant="body2">{f.nomeAluno ?? ""}</Typography>
          );
          if (i === 4) return f.exercicios.length;
          // datas ISO (date-only): dayjs parseia como local; new Date faria UTC e poderia recuar 1 dia no fuso BR
          if (i === 5) return (
            <Typography variant="body2">{f.dataInicio ? dayjs(f.dataInicio).format("DD/MM/YYYY") : ""}</Typography>
          );
          if (i === 6) return (
            <Typography variant="body2">{f.dataFim ? dayjs(f.dataFim).format("DD/MM/YYYY") : ""}</Typography>
          );
          if (i === 7) return (
            <Typography variant="body2">{new Date(f.createdAt).toLocaleDateString("pt-BR")}</Typography>
          );
          return (
            <Box sx={{ display: "flex", gap: 0.5, justifyContent: "center" }}>
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

      <Dialog open={createOpen} onClose={() => { closeCreate(); resetForm(); }} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Nova ficha de treino</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <FormControl size="small" fullWidth>
              <InputLabel shrink id="aluno-label">Aluno (opcional)</InputLabel>
              <Select
                labelId="aluno-label"
                value={selectedAlunoId}
                label="Aluno (opcional)"
                notched
                displayEmpty
                onChange={(e) => setSelectedAlunoId(e.target.value)}
              >
                {alunos.map((a) => (
                  <MenuItem key={a.alunoId} value={a.alunoId}>{a.nome}</MenuItem>
                ))}
              </Select>
              <FormHelperText>Deixe em branco para criar sem vincular. Vincule depois pela página do aluno.</FormHelperText>
            </FormControl>
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
            <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
              <DatePicker
                label="Início (opcional)"
                format="DD/MM/YYYY"
                value={dataInicio ? dayjs(dataInicio) : null}
                onChange={(d) => setDataInicio(toIsoDate(d))}
                slotProps={DATE_FIELD_SLOTS}
              />
              <DatePicker
                label="Validade (opcional)"
                format="DD/MM/YYYY"
                value={dataFim ? dayjs(dataFim) : null}
                onChange={(d) => setDataFim(toIsoDate(d))}
                slotProps={DATE_FIELD_SLOTS}
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

      <Dialog open={!!editTarget} onClose={closeEdit} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
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
            <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
              <DatePicker
                label="Início (opcional)"
                format="DD/MM/YYYY"
                value={editDataInicio ? dayjs(editDataInicio) : null}
                onChange={(d) => setEditDataInicio(toIsoDate(d))}
                slotProps={DATE_FIELD_SLOTS}
              />
              <DatePicker
                label="Validade (opcional)"
                format="DD/MM/YYYY"
                value={editDataFim ? dayjs(editDataFim) : null}
                onChange={(d) => setEditDataFim(toIsoDate(d))}
                slotProps={DATE_FIELD_SLOTS}
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
