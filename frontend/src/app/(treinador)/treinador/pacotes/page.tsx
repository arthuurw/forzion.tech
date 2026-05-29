"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, Card, CardContent, Grid, Button,
  Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Stack, IconButton, Tooltip,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { treinadorApi } from "@/lib/api/treinador";
import { extractApiError } from "@/lib/api/extractApiError";
import type { PacoteResponse } from "@/types";

export default function PacotesTreinadorPage() {
  const [pacotes, setPacotes] = useState<PacoteResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  // criar
  const [open, setOpen] = useState(false);
  const [nome, setNome] = useState("");
  const [descricao, setDescricao] = useState("");
  const [preco, setPreco] = useState("");
  const [saving, setSaving] = useState(false);

  // excluir
  const [deleteTarget, setDeleteTarget] = useState<PacoteResponse | null>(null);
  const [deleting, setDeleting] = useState(false);

  // editar
  const [editTarget, setEditTarget] = useState<PacoteResponse | null>(null);
  const [editNome, setEditNome] = useState("");
  const [editDescricao, setEditDescricao] = useState("");
  const [editPreco, setEditPreco] = useState("");
  const [editSaving, setEditSaving] = useState(false);

  const load = async () => {
    setLoading(true);
    try {
      const res = await treinadorApi.listPacotes();
      setPacotes(res.data);
    } catch (err) {
      setError(extractApiError(err, "Erro ao carregar pacotes."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const resetForm = () => { setNome(""); setDescricao(""); setPreco(""); };

  const handleCriar = async () => {
    if (!nome.trim() || !preco) return;
    setSaving(true);
    try {
      await treinadorApi.criarPacote({
        nome: nome.trim(),
        preco: Number(preco),
        descricao: descricao.trim() || null,
      });
      setSuccess(`Pacote "${nome}" criado.`);
      setOpen(false);
      resetForm();
      load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao criar pacote."));
    } finally {
      setSaving(false);
    }
  };

  const openEdit = (p: PacoteResponse) => {
    setEditTarget(p);
    setEditNome(p.nome);
    setEditDescricao(p.descricao ?? "");
    setEditPreco(String(p.preco));
  };

  const handleExcluir = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await treinadorApi.excluirPacote(deleteTarget.pacoteId);
      setSuccess(`Pacote "${deleteTarget.nome}" excluído.`);
      setDeleteTarget(null);
      load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao excluir pacote."));
    } finally {
      setDeleting(false);
    }
  };

  const handleEditar = async () => {
    if (!editTarget || !editNome.trim() || !editPreco) return;
    setEditSaving(true);
    try {
      await treinadorApi.atualizarPacote(editTarget.pacoteId, {
        nome: editNome.trim(),
        preco: Number(editPreco),
        descricao: editDescricao.trim() || null,
      });
      setSuccess(`Pacote "${editNome}" atualizado.`);
      setEditTarget(null);
      load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao atualizar pacote."));
    } finally {
      setEditSaving(false);
    }
  };

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700 }}>Pacotes de Treinos</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>
          Novo pacote
        </Button>
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      {loading ? (
        <LoadingSpinner />
      ) : pacotes.length === 0 ? (
        <EmptyState
          message="Nenhum pacote cadastrado. Crie pacotes de treinos para oferecer aos seus alunos."
          actionLabel="Criar primeiro pacote"
          onAction={() => setOpen(true)}
        />
      ) : (
        <Grid container spacing={2}>
          {pacotes.map((p) => (
            <Grid key={p.pacoteId} size={{ xs: 12, sm: 6, md: 4 }}>
              <Card
                variant="outlined"
                onClick={() => openEdit(p)}
                sx={{ cursor: "pointer", "&:hover": { borderColor: "primary.main" } }}
              >
                <CardContent>
                  <Box sx={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between" }}>
                    <Typography variant="h6" sx={{ fontWeight: 700, mb: 0.5 }}>{p.nome}</Typography>
                    <Stack direction="row" spacing={0.5}>
                      <Tooltip title="Editar">
                        <IconButton
                          size="small"
                          onClick={(e) => { e.stopPropagation(); openEdit(p); }}
                          sx={{ mt: -0.5 }}
                        >
                          <EditIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title="Excluir">
                        <IconButton
                          size="small"
                          color="error"
                          onClick={(e) => { e.stopPropagation(); setDeleteTarget(p); }}
                          sx={{ mt: -0.5 }}
                        >
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </Stack>
                  </Box>
                  {p.descricao && (
                    <Typography variant="body2" color="text.secondary" sx={{ mb: 0.5 }}>
                      {p.descricao}
                    </Typography>
                  )}
                  <Typography variant="body2" color="text.secondary">
                    R$ {p.preco.toFixed(2)}/mês
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}

      {/* Dialog: criar */}
      <Dialog open={open} onClose={() => { setOpen(false); resetForm(); }} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Novo pacote</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField
              label="Nome"
              value={nome}
              onChange={(e) => setNome(e.target.value)}
              size="small"
              fullWidth
              required
              autoFocus
            />
            <TextField
              label="Descrição"
              value={descricao}
              onChange={(e) => setDescricao(e.target.value)}
              size="small"
              fullWidth
              multiline
              minRows={2}
              slotProps={{ htmlInput: { maxLength: 500 } }}
              helperText="Ex: Treino + acompanhamento via WhatsApp"
            />
            <TextField
              label="Preço mensal (R$)"
              type="number"
              value={preco}
              onChange={(e) => setPreco(e.target.value)}
              size="small"
              fullWidth
              required
              slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { setOpen(false); resetForm(); }}>Cancelar</Button>
          <Button
            variant="contained"
            disabled={!nome.trim() || !preco || saving}
            onClick={handleCriar}
          >
            Criar
          </Button>
        </DialogActions>
      </Dialog>

      {/* Dialog: excluir */}
      <Dialog open={!!deleteTarget} onClose={() => setDeleteTarget(null)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Excluir pacote</DialogTitle>
        <DialogContent>
          <Typography variant="body2">
            Tem certeza que deseja excluir <strong>{deleteTarget?.nome}</strong>?
            Esta ação não pode ser desfeita.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteTarget(null)}>Cancelar</Button>
          <Button
            variant="contained"
            color="error"
            disabled={deleting}
            onClick={handleExcluir}
          >
            Excluir
          </Button>
        </DialogActions>
      </Dialog>

      {/* Dialog: editar */}
      <Dialog open={!!editTarget} onClose={() => setEditTarget(null)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Editar pacote</DialogTitle>
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
            <TextField
              label="Descrição"
              value={editDescricao}
              onChange={(e) => setEditDescricao(e.target.value)}
              size="small"
              fullWidth
              multiline
              minRows={2}
              slotProps={{ htmlInput: { maxLength: 500 } }}
            />
            <TextField
              label="Preço mensal (R$)"
              type="number"
              value={editPreco}
              onChange={(e) => setEditPreco(e.target.value)}
              size="small"
              fullWidth
              required
              slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditTarget(null)}>Cancelar</Button>
          <Button
            variant="contained"
            disabled={!editNome.trim() || !editPreco || editSaving}
            onClick={handleEditar}
          >
            Salvar
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
