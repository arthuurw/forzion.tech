"use client";
import { useEffect, useState } from "react";
import { useForm, FormProvider, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  Box, Typography, Card, CardContent, CardActions, Grid, Button, Chip,
  Dialog, DialogTitle, DialogContent, DialogActions,
  Stack, IconButton, Tooltip, TextField, MenuItem,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import AlertBanner from "@/components/ui/AlertBanner";
import PageHeader from "@/components/ui/PageHeader";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import FormTextField from "@/components/forms/FormTextField";
import { adminApi } from "@/lib/api/admin";
import type { PlanoPlataformaResponse, TierPlano } from "@/types";
import { formatarBRL } from "@/lib/utils/formatting";
import { extractApiError } from "@/lib/api/extractApiError";

const TIER_OPTIONS: { value: TierPlano; label: string; disabled?: boolean }[] = [
  { value: "Free",    label: "Free" },
  { value: "Basic",   label: "Basic" },
  { value: "Pro",     label: "Pro" },
  { value: "ProPlus", label: "Pro Plus" },
  { value: "Elite",   label: "Elite (em breve)", disabled: true },
];

const planoSchema = z.object({
  nome: z.string().trim().min(1, "Informe o nome."),
  tier: z.string().min(1, "Selecione o tier."),
  maxAlunos: z.coerce.number().int().min(1, "Mínimo 1 aluno."),
  preco: z.coerce.number().min(0, "Preço não pode ser negativo."),
  descricao: z.string(),
});
type PlanoForm = z.infer<typeof planoSchema>;

const DEFAULT_CRIAR: PlanoForm = { nome: "", tier: "Basic", maxAlunos: 1, preco: 0, descricao: "" };

export default function PlanosAdminPage() {
  const [planos, setPlanos] = useState<PlanoPlataformaResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [criarOpen, setCriarOpen] = useState(false);
  const [saving, setSaving] = useState(false);

  const [editPlano, setEditPlano] = useState<PlanoPlataformaResponse | null>(null);
  const [savingEdit, setSavingEdit] = useState(false);

  const [confirmExcluir, setConfirmExcluir] = useState<PlanoPlataformaResponse | null>(null);
  const [loadingExcluir, setLoadingExcluir] = useState(false);

  const criarForm = useForm<z.input<typeof planoSchema>, unknown, PlanoForm>({
    resolver: zodResolver(planoSchema),
    defaultValues: DEFAULT_CRIAR,
  });

  const editForm = useForm<z.input<typeof planoSchema>, unknown, PlanoForm>({
    resolver: zodResolver(planoSchema),
    defaultValues: DEFAULT_CRIAR,
  });

  const load = async () => {
    setLoading(true);
    try {
      const res = await adminApi.listPlanos();
      setPlanos(res.data);
    } catch (err) {
      setError(extractApiError(err, "Erro ao carregar planos."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const handleCriar = criarForm.handleSubmit(async (data) => {
    setSaving(true);
    try {
      await adminApi.criarPlano(data.nome, data.tier as TierPlano, data.maxAlunos, data.preco, data.descricao.trim() || undefined);
      setSuccess(`Plano "${data.nome}" criado.`);
      setCriarOpen(false);
      criarForm.reset(DEFAULT_CRIAR);
      load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao criar plano."));
    } finally {
      setSaving(false);
    }
  });

  const openEdit = (p: PlanoPlataformaResponse) => {
    setEditPlano(p);
    editForm.reset({ nome: p.nome, tier: p.tier, maxAlunos: p.maxAlunos, preco: p.preco, descricao: p.descricao ?? "" });
  };

  const handleEditar = editForm.handleSubmit(async (data) => {
    if (!editPlano) return;
    setSavingEdit(true);
    try {
      await adminApi.atualizarPlano(editPlano.planoId, {
        nome: data.nome,
        tier: data.tier as TierPlano,
        maxAlunos: data.maxAlunos,
        preco: data.preco,
        descricao: data.descricao.trim() || null,
      });
      setSuccess(`Plano "${data.nome}" atualizado.`);
      setEditPlano(null);
      load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao atualizar plano."));
    } finally {
      setSavingEdit(false);
    }
  });

  const handleExcluir = async () => {
    if (!confirmExcluir) return;
    setLoadingExcluir(true);
    try {
      await adminApi.excluirPlano(confirmExcluir.planoId);
      setSuccess(`Plano "${confirmExcluir.nome}" excluído.`);
      setConfirmExcluir(null);
      load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao excluir plano."));
    } finally {
      setLoadingExcluir(false);
    }
  };

  return (
    <Box>
      <PageHeader
        title="Planos"
        action={
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCriarOpen(true)}>
            Novo plano
          </Button>
        }
      />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      {loading ? (
        <LoadingSpinner />
      ) : planos.length === 0 ? (
        <EmptyState
          message="Nenhum plano cadastrado."
          actionLabel="Criar primeiro plano"
          onAction={() => setCriarOpen(true)}
        />
      ) : (
        <Grid container spacing={2}>
          {planos.map((p) => (
            <Grid key={p.planoId} size={{ xs: 12, sm: 6, md: 4 }}>
              <Card variant="outlined">
                <CardContent>
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.5 }}>
                    <Typography variant="h6">{p.nome}</Typography>
                    <Chip label={p.tier} size="small" variant="outlined" />
                  </Box>
                  <Typography variant="body2" color="text.secondary">Até {p.maxAlunos} alunos</Typography>
                  <Typography variant="body2" color="text.secondary">
                    {Number(p.preco) === 0 ? "Gratuito" : `${formatarBRL(Number(p.preco))}/mês`}
                  </Typography>
                  {p.descricao && (
                    <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, fontStyle: "italic" }}>
                      {p.descricao}
                    </Typography>
                  )}
                  <Chip
                    label={p.isAtivo ? "Ativo" : "Inativo"}
                    size="small"
                    color={p.isAtivo ? "success" : "default"}
                    sx={{ mt: 1 }}
                  />
                </CardContent>
                <CardActions sx={{ justifyContent: "flex-end", pt: 0 }}>
                  <Tooltip title="Editar">
                    <IconButton size="small" onClick={() => openEdit(p)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Excluir">
                    <IconButton size="small" color="error" onClick={() => setConfirmExcluir(p)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </CardActions>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}

      <Dialog
        open={criarOpen}
        onClose={() => { setCriarOpen(false); criarForm.reset(DEFAULT_CRIAR); }}
        maxWidth="xs"
        fullWidth
        slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}
      >
        <DialogTitle>Novo plano</DialogTitle>
        <FormProvider {...criarForm}>
          <Stack component="form" onSubmit={handleCriar} noValidate>
            <DialogContent>
              <Stack spacing={2} sx={{ pt: 1 }}>
                <FormTextField name="nome" label="Nome" size="small" fullWidth autoFocus required />
                <Controller
                  name="tier"
                  control={criarForm.control}
                  render={({ field }) => (
                    <TextField {...field} select label="Tier" size="small" fullWidth required>
                      {TIER_OPTIONS.map((o) => (
                        <MenuItem key={o.value} value={o.value} disabled={o.disabled}>{o.label}</MenuItem>
                      ))}
                    </TextField>
                  )}
                />
                <FormTextField name="maxAlunos" label="Máximo de alunos" type="number" size="small" fullWidth required />
                <FormTextField name="preco" label="Preço (R$)" type="number" size="small" fullWidth required />
                <FormTextField name="descricao" label="Descrição (funcionalidades)" size="small" fullWidth multiline rows={2} placeholder="Ex: Basic + e-mail" />
              </Stack>
            </DialogContent>
            <DialogActions>
              <Button onClick={() => { setCriarOpen(false); criarForm.reset(DEFAULT_CRIAR); }}>Cancelar</Button>
              <Button type="submit" variant="contained" disabled={saving}>Criar</Button>
            </DialogActions>
          </Stack>
        </FormProvider>
      </Dialog>

      <Dialog
        open={!!editPlano}
        onClose={() => { setEditPlano(null); editForm.reset(DEFAULT_CRIAR); }}
        maxWidth="xs"
        fullWidth
        slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}
      >
        <DialogTitle>Editar — {editPlano?.nome}</DialogTitle>
        <FormProvider {...editForm}>
          <Stack component="form" onSubmit={handleEditar} noValidate>
            <DialogContent>
              <Stack spacing={2} sx={{ pt: 1 }}>
                <FormTextField name="nome" label="Nome" size="small" fullWidth autoFocus required />
                <Controller
                  name="tier"
                  control={editForm.control}
                  render={({ field }) => (
                    <TextField {...field} select label="Tier" size="small" fullWidth required>
                      {TIER_OPTIONS.map((o) => (
                        <MenuItem key={o.value} value={o.value} disabled={o.disabled}>{o.label}</MenuItem>
                      ))}
                    </TextField>
                  )}
                />
                <FormTextField name="maxAlunos" label="Máximo de alunos" type="number" size="small" fullWidth required />
                <FormTextField name="preco" label="Preço (R$)" type="number" size="small" fullWidth required />
                <FormTextField name="descricao" label="Descrição (funcionalidades)" size="small" fullWidth multiline rows={2} placeholder="Ex: Basic + e-mail" />
              </Stack>
            </DialogContent>
            <DialogActions>
              <Button onClick={() => { setEditPlano(null); editForm.reset(DEFAULT_CRIAR); }}>Cancelar</Button>
              <Button type="submit" variant="contained" disabled={savingEdit}>Salvar</Button>
            </DialogActions>
          </Stack>
        </FormProvider>
      </Dialog>

      <ConfirmDialog
        open={!!confirmExcluir}
        title="Excluir plano"
        description={`Excluir "${confirmExcluir?.nome}"? Treinadores com este plano atribuído não serão afetados.`}
        confirmLabel="Excluir"
        destructive
        loading={loadingExcluir}
        onConfirm={handleExcluir}
        onClose={() => setConfirmExcluir(null)}
      />
    </Box>
  );
}
