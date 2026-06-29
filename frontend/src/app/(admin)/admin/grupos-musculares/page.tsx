"use client";
import { useCallback, useEffect, useState } from "react";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  Box, Typography, Card, Button, Dialog, DialogTitle, DialogContent, DialogActions,
  Stack, IconButton, Tooltip,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import FormTextField from "@/components/forms/FormTextField";
import AlertBanner from "@/components/ui/AlertBanner";
import PageHeader from "@/components/ui/PageHeader";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import { adminApi } from "@/lib/api/admin";
import type { GrupoMuscularResponse } from "@/types";
import { extractApiError } from "@/lib/api/extractApiError";

const grupoSchema = z.object({
  nome: z.string().trim().min(1, "Informe o nome."),
});
type GrupoForm = z.infer<typeof grupoSchema>;

const COLUMNS: Column[] = [
  { label: "Nome" },
  { label: "Criado em" },
  { label: "Ações", align: "right" },
];

export default function GruposMuscularesAdminPage() {
  const [grupos, setGrupos] = useState<GrupoMuscularResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [criarOpen, setCriarOpen] = useState(false);
  const [saving, setSaving] = useState(false);

  const [editGrupo, setEditGrupo] = useState<GrupoMuscularResponse | null>(null);
  const [savingEdit, setSavingEdit] = useState(false);

  const [confirmExcluir, setConfirmExcluir] = useState<GrupoMuscularResponse | null>(null);
  const [loadingExcluir, setLoadingExcluir] = useState(false);

  const criarForm = useForm<GrupoForm>({
    resolver: zodResolver(grupoSchema),
    defaultValues: { nome: "" },
  });

  const editForm = useForm<GrupoForm>({
    resolver: zodResolver(grupoSchema),
    defaultValues: { nome: "" },
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await adminApi.listGruposMusculares();
      setGrupos(res.data);
    } catch (err) {
      setError(extractApiError(err, "Erro ao carregar grupos musculares."));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const handleCriar = criarForm.handleSubmit(async (data) => {
    setSaving(true);
    try {
      await adminApi.criarGrupoMuscular(data.nome);
      setSuccess(`"${data.nome}" adicionado.`);
      setCriarOpen(false);
      criarForm.reset();
      load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao criar grupo muscular."));
    } finally {
      setSaving(false);
    }
  });

  const openEdit = (g: GrupoMuscularResponse) => {
    setEditGrupo(g);
    editForm.reset({ nome: g.nome });
  };

  const handleEditar = editForm.handleSubmit(async (data) => {
    if (!editGrupo) return;
    setSavingEdit(true);
    try {
      await adminApi.atualizarGrupoMuscular(editGrupo.id, data.nome);
      setSuccess(`"${data.nome}" atualizado.`);
      setEditGrupo(null);
      load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao atualizar grupo muscular."));
    } finally {
      setSavingEdit(false);
    }
  });

  const handleExcluir = async () => {
    if (!confirmExcluir) return;
    setLoadingExcluir(true);
    try {
      await adminApi.excluirGrupoMuscular(confirmExcluir.id);
      setSuccess(`"${confirmExcluir.nome}" excluído.`);
      setConfirmExcluir(null);
      load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao excluir grupo muscular. Pode estar vinculado a exercícios."));
    } finally {
      setLoadingExcluir(false);
    }
  };

  return (
    <Box>
      <PageHeader
        title="Grupos Musculares"
        subtitle="Padronização para a biblioteca de exercícios."
        action={
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCriarOpen(true)}>
            Novo grupo
          </Button>
        }
      />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Card variant="outlined">
        {loading ? <LoadingSpinner /> : grupos.length === 0 ? (
          <EmptyState message="Nenhum grupo encontrado." actionLabel="Adicionar grupo" onAction={() => setCriarOpen(true)} />
        ) : (
          <ResponsiveTable
            columns={COLUMNS}
            rows={grupos}
            rowKey={(g) => g.id}
            renderCell={(g, i) => {
              if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{g.nome}</Typography>;
              if (i === 1) return new Date(g.createdAt).toLocaleDateString("pt-BR");
              return (
                <>
                  <Tooltip title="Editar">
                    <IconButton size="small" aria-label="Editar grupo muscular" onClick={() => openEdit(g)}><EditIcon fontSize="small" /></IconButton>
                  </Tooltip>
                  <Tooltip title="Excluir">
                    <IconButton size="small" color="error" aria-label="Excluir grupo muscular" onClick={() => setConfirmExcluir(g)}><DeleteIcon fontSize="small" /></IconButton>
                  </Tooltip>
                </>
              );
            }}
          />
        )}
      </Card>

      <Dialog open={criarOpen} onClose={() => { setCriarOpen(false); criarForm.reset(); }} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Novo grupo muscular</DialogTitle>
        <FormProvider {...criarForm}>
          <Stack component="form" onSubmit={handleCriar} noValidate>
            <DialogContent>
              <Stack spacing={2} sx={{ pt: 1 }}>
                <FormTextField name="nome" label="Nome" size="small" fullWidth autoFocus required />
              </Stack>
            </DialogContent>
            <DialogActions>
              <Button onClick={() => { setCriarOpen(false); criarForm.reset(); }}>Cancelar</Button>
              <Button type="submit" variant="contained" disabled={saving}>Adicionar</Button>
            </DialogActions>
          </Stack>
        </FormProvider>
      </Dialog>

      <Dialog open={!!editGrupo} onClose={() => { setEditGrupo(null); editForm.reset(); }} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Editar — {editGrupo?.nome}</DialogTitle>
        <FormProvider {...editForm}>
          <Stack component="form" onSubmit={handleEditar} noValidate>
            <DialogContent>
              <Stack spacing={2} sx={{ pt: 1 }}>
                <FormTextField name="nome" label="Nome" size="small" fullWidth required />
              </Stack>
            </DialogContent>
            <DialogActions>
              <Button onClick={() => { setEditGrupo(null); editForm.reset(); }}>Cancelar</Button>
              <Button type="submit" variant="contained" disabled={savingEdit}>Salvar</Button>
            </DialogActions>
          </Stack>
        </FormProvider>
      </Dialog>

      <ConfirmDialog
        open={!!confirmExcluir}
        title="Excluir grupo"
        description={`Excluir "${confirmExcluir?.nome}"? Isso pode afetar exercícios que dependem deste grupo.`}
        confirmLabel="Excluir"
        destructive
        loading={loadingExcluir}
        onConfirm={handleExcluir}
        onClose={() => setConfirmExcluir(null)}
      />
    </Box>
  );
}
