"use client";
import { useCallback, useEffect, useState } from "react";
import { useForm, FormProvider, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useQuery } from "@tanstack/react-query";
import { queryKeys } from "@/lib/query/keys";
import {
  Box, Typography, Button, Dialog, DialogTitle, DialogContent, DialogActions,
  Stack, TextField, Select, MenuItem, FormControl, InputLabel, IconButton, Tooltip,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import AlertBanner from "@/components/ui/AlertBanner";
import PageHeader from "@/components/ui/PageHeader";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import DataList from "@/components/ui/DataList";
import type { Column } from "@/components/ui/ResponsiveTable";
import FormTextField from "@/components/forms/FormTextField";
import FormSelect from "@/components/forms/FormSelect";
import { adminApi } from "@/lib/api/admin";
import { parseYouTubeId } from "@/lib/utils/youtube";
import type { ExercicioResponse } from "@/types";
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

const exercicioSchema = z.object({
  nome: z.string().trim().min(1, "Informe o nome."),
  grupoMuscularId: z.string().min(1, "Selecione o grupo muscular."),
  descricao: z.string().optional(),
  comoExecutar: z.string().max(2000, "Máximo 2000 caracteres.").optional(),
  videoUrl: z.string().optional(),
});
type ExercicioForm = z.infer<typeof exercicioSchema>;

export default function ExerciciosAdminPage() {
  const { data: grupos = [], isError: gruposError } = useQuery({
    queryKey: queryKeys.catalog.gruposMusculares,
    queryFn: () => adminApi.listGruposMusculares().then((r) => r.data),
    staleTime: 30 * 60 * 1000,
    gcTime: 30 * 60 * 1000,
  });

  const [filtroNome, setFiltroNome] = useState("");
  const [filtroGrupo, setFiltroGrupo] = useState("");
  const [ordenarPor, setOrdenarPor] = useState<"nome" | "grupoMuscular">("nome");

  const {
    createOpen: criarOpen, openCreate: openCriar, closeCreate: closeCriar, creating: saving, setCreating: setSaving,
    editTarget: editEx, openEdit, closeEdit, editing: savingEdit, setEditing: setSavingEdit,
    deleteTarget: confirmExcluir, openDelete: openExcluir, closeDelete: closeExcluir,
    deleting: loadingExcluir, setDeleting: setLoadingExcluir,
  } = useCRUDDialog<ExercicioResponse>();

  const criarForm = useForm<ExercicioForm>({
    resolver: zodResolver(exercicioSchema),
    defaultValues: { nome: "", grupoMuscularId: "", descricao: "", comoExecutar: "", videoUrl: "" },
  });

  const editForm = useForm<ExercicioForm>({
    resolver: zodResolver(exercicioSchema),
    defaultValues: { nome: "", grupoMuscularId: "", descricao: "", comoExecutar: "", videoUrl: "" },
  });

  const grupoOptions = grupos.map((g) => ({ value: g.id, label: g.nome }));

  const videoUrlWatch = criarForm.watch("videoUrl") ?? "";
  const videoUrlInvalido = !!videoUrlWatch && parseYouTubeId(videoUrlWatch) === null;
  const editVideoUrlWatch = editForm.watch("videoUrl") ?? "";
  const editVideoUrlInvalido = !!editVideoUrlWatch && parseYouTubeId(editVideoUrlWatch) === null;

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

  useEffect(() => {
    if (gruposError) {
      setError("Não foi possível carregar os grupos musculares. O cadastro de exercícios fica indisponível.");
    }
  }, [gruposError, setError]);

  useEffect(() => {
    if (!criarOpen || grupos.length === 0) return;
    if (!criarForm.getValues("grupoMuscularId")) criarForm.setValue("grupoMuscularId", grupos[0].id);
  }, [criarOpen, grupos]);

  const handleCriar = criarForm.handleSubmit(async (data) => {
    setSaving(true);
    try {
      await adminApi.criarExercicioGlobal({
        nome: data.nome,
        grupoMuscularId: data.grupoMuscularId,
        descricao: data.descricao?.trim() || null,
        comoExecutar: data.comoExecutar?.trim() || null,
        videoUrl: data.videoUrl?.trim() || null,
      });
      setSuccess(`"${data.nome}" adicionado.`);
      closeCriar();
      criarForm.reset();
      reload();
    } catch (err) {
      setError(extractApiError(err, "Erro ao criar exercício."));
    } finally {
      setSaving(false);
    }
  });

  const handleOpenEdit = (ex: ExercicioResponse) => {
    openEdit(ex);
    editForm.reset({
      nome: ex.nome,
      grupoMuscularId: ex.grupoMuscularId,
      descricao: ex.descricao ?? "",
      comoExecutar: ex.comoExecutar ?? "",
      videoUrl: ex.videoId ?? "",
    });
  };

  const handleEditar = editForm.handleSubmit(async (data) => {
    if (!editEx) return;
    setSavingEdit(true);
    try {
      await adminApi.atualizarExercicioGlobal(editEx.exercicioId, {
        nome: data.nome || undefined,
        grupoMuscularId: data.grupoMuscularId || undefined,
        descricao: data.descricao?.trim() || null,
        comoExecutar: data.comoExecutar?.trim() ?? "",
        videoUrl: data.videoUrl?.trim() ?? "",
      });
      setSuccess(`"${data.nome}" atualizado.`);
      closeEdit();
      reload();
    } catch (err) {
      setError(extractApiError(err, "Erro ao atualizar exercício."));
    } finally {
      setSavingEdit(false);
    }
  });

  const handleExcluir = async () => {
    if (!confirmExcluir) return;
    setLoadingExcluir(true);
    try {
      await adminApi.excluirExercicioGlobal(confirmExcluir.exercicioId);
      setSuccess(`"${confirmExcluir.nome}" excluído.`);
      closeExcluir();
      reload();
    } catch (err) {
      setError(extractApiError(err, "Erro ao excluir exercício. Pode estar em uso em fichas."));
    } finally {
      setLoadingExcluir(false);
    }
  };

  return (
    <Box>
      <PageHeader
        title="Biblioteca Global"
        subtitle="Exercícios disponíveis para todos os treinadores copiarem."
        action={
          <Button variant="contained" startIcon={<AddIcon />} onClick={openCriar}>
            Novo exercício
          </Button>
        }
      />

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

      <Dialog open={criarOpen} onClose={() => { closeCriar(); criarForm.reset(); }} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Novo exercício global</DialogTitle>
        <FormProvider {...criarForm}>
          <Stack component="form" onSubmit={handleCriar} noValidate>
            <DialogContent>
              <Stack spacing={2} sx={{ pt: 1 }}>
                <FormTextField name="nome" label="Nome" size="small" fullWidth required autoFocus />
                <FormSelect name="grupoMuscularId" label="Grupo muscular" options={grupoOptions} required />
                <FormTextField name="descricao" label="Descrição (opcional)" size="small" fullWidth multiline rows={3} />
                <FormTextField name="comoExecutar" label="Como executar (opcional)" size="small" fullWidth multiline rows={3} slotProps={{ htmlInput: { maxLength: 2000 } }} />
                <Controller
                  name="videoUrl"
                  control={criarForm.control}
                  render={({ field }) => (
                    <TextField
                      {...field}
                      label="Link do vídeo (YouTube, opcional)"
                      size="small"
                      fullWidth
                      error={videoUrlInvalido}
                      helperText={videoUrlInvalido ? "Informe um link ou ID de vídeo do YouTube válido." : " "}
                    />
                  )}
                />
              </Stack>
            </DialogContent>
            <DialogActions>
              <Button onClick={() => { closeCriar(); criarForm.reset(); }}>Cancelar</Button>
              <Button type="submit" variant="contained" disabled={saving || videoUrlInvalido}>Adicionar</Button>
            </DialogActions>
          </Stack>
        </FormProvider>
      </Dialog>

      <Dialog open={!!editEx} onClose={closeEdit} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Editar — {editEx?.nome}</DialogTitle>
        <FormProvider {...editForm}>
          <Stack component="form" onSubmit={handleEditar} noValidate>
            <DialogContent>
              <Stack spacing={2} sx={{ pt: 1 }}>
                <FormTextField name="nome" label="Nome" size="small" fullWidth required />
                <FormSelect name="grupoMuscularId" label="Grupo muscular" options={grupoOptions} required />
                <FormTextField name="descricao" label="Descrição" size="small" fullWidth multiline rows={3} />
                <FormTextField name="comoExecutar" label="Como executar" size="small" fullWidth multiline rows={3} slotProps={{ htmlInput: { maxLength: 2000 } }} />
                <Controller
                  name="videoUrl"
                  control={editForm.control}
                  render={({ field }) => (
                    <TextField
                      {...field}
                      label="Link do vídeo (YouTube)"
                      size="small"
                      fullWidth
                      error={editVideoUrlInvalido}
                      helperText={editVideoUrlInvalido ? "Informe um link ou ID de vídeo do YouTube válido." : " "}
                    />
                  )}
                />
              </Stack>
            </DialogContent>
            <DialogActions>
              <Button onClick={closeEdit}>Cancelar</Button>
              <Button type="submit" variant="contained" disabled={savingEdit || editVideoUrlInvalido}>Salvar</Button>
            </DialogActions>
          </Stack>
        </FormProvider>
      </Dialog>

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
