"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, Card, CardContent, CardActions, Grid, Button, Chip,
  Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Stack, IconButton, Tooltip, MenuItem,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import { adminApi } from "@/lib/api/admin";
import type { PlanoTreinadorResponse, TierPlano } from "@/types";

const TIER_OPTIONS: { value: TierPlano; label: string }[] = [
  { value: "Free",    label: "Free" },
  { value: "Basic",   label: "Basic" },
  { value: "Pro",     label: "Pro" },
  { value: "ProPlus", label: "Pro Plus" },
  { value: "Elite",   label: "Elite" },
];

export default function PlanosAdminPage() {
  const [planos, setPlanos] = useState<PlanoTreinadorResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  // criar
  const [criarOpen, setCriarOpen] = useState(false);
  const [nome, setNome] = useState("");
  const [tier, setTier] = useState<TierPlano>("Basic");
  const [maxAlunos, setMaxAlunos] = useState("");
  const [preco, setPreco] = useState("");
  const [descricao, setDescricao] = useState("");
  const [saving, setSaving] = useState(false);

  // editar
  const [editPlano, setEditPlano] = useState<PlanoTreinadorResponse | null>(null);
  const [editNome, setEditNome] = useState("");
  const [editTier, setEditTier] = useState<TierPlano>("Basic");
  const [editMaxAlunos, setEditMaxAlunos] = useState("");
  const [editPreco, setEditPreco] = useState("");
  const [editDescricao, setEditDescricao] = useState("");
  const [savingEdit, setSavingEdit] = useState(false);

  // excluir
  const [confirmExcluir, setConfirmExcluir] = useState<PlanoTreinadorResponse | null>(null);
  const [loadingExcluir, setLoadingExcluir] = useState(false);

  const load = async () => {
    setLoading(true);
    try {
      const res = await adminApi.listPlanos();
      setPlanos(res.data);
    } catch {
      setError("Erro ao carregar planos.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const handleCriar = async () => {
    if (!nome.trim() || !maxAlunos || preco === "") return;
    setSaving(true);
    try {
      await adminApi.criarPlano(nome.trim(), tier, Number(maxAlunos), Number(preco), descricao.trim() || undefined);
      setSuccess(`Plano "${nome}" criado.`);
      setCriarOpen(false);
      setNome(""); setTier("Basic"); setMaxAlunos(""); setPreco(""); setDescricao("");
      load();
    } catch {
      setError("Erro ao criar plano.");
    } finally {
      setSaving(false);
    }
  };

  const openEdit = (p: PlanoTreinadorResponse) => {
    setEditPlano(p);
    setEditNome(p.nome);
    setEditTier(p.tier);
    setEditMaxAlunos(String(p.maxAlunos));
    setEditPreco(String(p.preco));
    setEditDescricao(p.descricao ?? "");
  };

  const handleEditar = async () => {
    if (!editPlano) return;
    setSavingEdit(true);
    try {
      await adminApi.atualizarPlano(editPlano.planoId, {
        nome: editNome.trim() || undefined,
        tier: editTier,
        maxAlunos: editMaxAlunos ? Number(editMaxAlunos) : undefined,
        preco: editPreco !== "" ? Number(editPreco) : undefined,
        descricao: editDescricao.trim() || null,
      });
      setSuccess(`Plano "${editNome}" atualizado.`);
      setEditPlano(null);
      load();
    } catch {
      setError("Erro ao atualizar plano.");
    } finally {
      setSavingEdit(false);
    }
  };

  const handleExcluir = async () => {
    if (!confirmExcluir) return;
    setLoadingExcluir(true);
    try {
      await adminApi.excluirPlano(confirmExcluir.planoId);
      setSuccess(`Plano "${confirmExcluir.nome}" excluído.`);
      setConfirmExcluir(null);
      load();
    } catch {
      setError("Erro ao excluir plano.");
    } finally {
      setLoadingExcluir(false);
    }
  };

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700 }}>Planos</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCriarOpen(true)}>
          Novo plano
        </Button>
      </Box>

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
                    <Typography variant="h6" sx={{ fontWeight: 700 }}>{p.nome}</Typography>
                    <Chip label={p.tier} size="small" variant="outlined" />
                  </Box>
                  <Typography variant="body2" color="text.secondary">Até {p.maxAlunos} alunos</Typography>
                  <Typography variant="body2" color="text.secondary">
                    {Number(p.preco) === 0 ? "Gratuito" : `R$ ${Number(p.preco).toFixed(2)}/mês`}
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

      {/* Criar */}
      <Dialog open={criarOpen} onClose={() => setCriarOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Novo plano</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField label="Nome" value={nome} onChange={(e) => setNome(e.target.value)} size="small" fullWidth required />
            <TextField
              select label="Tier" value={tier} onChange={(e) => setTier(e.target.value as TierPlano)}
              size="small" fullWidth required
            >
              {TIER_OPTIONS.map((o) => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
            </TextField>
            <TextField label="Máximo de alunos" type="number" value={maxAlunos} onChange={(e) => setMaxAlunos(e.target.value)} size="small" fullWidth required slotProps={{ htmlInput: { min: 1 } }} />
            <TextField label="Preço (R$)" type="number" value={preco} onChange={(e) => setPreco(e.target.value)} size="small" fullWidth required slotProps={{ htmlInput: { min: 0, step: 0.01 } }} />
            <TextField label="Descrição (funcionalidades)" value={descricao} onChange={(e) => setDescricao(e.target.value)} size="small" fullWidth multiline rows={2} placeholder="Ex: Basic + e-mail" />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCriarOpen(false)}>Cancelar</Button>
          <Button variant="contained" disabled={!nome.trim() || !maxAlunos || preco === "" || saving} onClick={handleCriar}>
            Criar
          </Button>
        </DialogActions>
      </Dialog>

      {/* Editar */}
      <Dialog open={!!editPlano} onClose={() => setEditPlano(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Editar — {editPlano?.nome}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField label="Nome" value={editNome} onChange={(e) => setEditNome(e.target.value)} size="small" fullWidth />
            <TextField
              select label="Tier" value={editTier} onChange={(e) => setEditTier(e.target.value as TierPlano)}
              size="small" fullWidth
            >
              {TIER_OPTIONS.map((o) => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
            </TextField>
            <TextField label="Máximo de alunos" type="number" value={editMaxAlunos} onChange={(e) => setEditMaxAlunos(e.target.value)} size="small" fullWidth slotProps={{ htmlInput: { min: 1 } }} />
            <TextField label="Preço (R$)" type="number" value={editPreco} onChange={(e) => setEditPreco(e.target.value)} size="small" fullWidth slotProps={{ htmlInput: { min: 0, step: 0.01 } }} />
            <TextField label="Descrição (funcionalidades)" value={editDescricao} onChange={(e) => setEditDescricao(e.target.value)} size="small" fullWidth multiline rows={2} placeholder="Ex: Basic + e-mail" />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditPlano(null)}>Cancelar</Button>
          <Button variant="contained" disabled={savingEdit} onClick={handleEditar}>
            Salvar
          </Button>
        </DialogActions>
      </Dialog>

      {/* Confirmar exclusão */}
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
