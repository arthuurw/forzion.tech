"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, Card, CardContent, Grid, Button,
  Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Stack, IconButton, Tooltip, Switch, FormControlLabel, Chip,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import PageHeader from "@/components/ui/PageHeader";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import { treinadorApi } from "@/lib/api/treinador";
import { extractApiError } from "@/lib/api/extractApiError";
import type { PacoteResponse } from "@/types";
import { formatarBRL } from "@/lib/utils/formatting";

export default function PacotesTreinadorPage() {
  const [pacotes, setPacotes] = useState<PacoteResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [open, setOpen] = useState(false);
  const [nome, setNome] = useState("");
  const [descricao, setDescricao] = useState("");
  const [preco, setPreco] = useState("");
  const [categoria, setCategoria] = useState("");
  const [duracaoMinutos, setDuracaoMinutos] = useState("");
  const [trialDisponivel, setTrialDisponivel] = useState(false);
  const [publico, setPublico] = useState(false);
  const [saving, setSaving] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState<PacoteResponse | null>(null);
  const [deleting, setDeleting] = useState(false);

  const [editTarget, setEditTarget] = useState<PacoteResponse | null>(null);
  const [editNome, setEditNome] = useState("");
  const [editDescricao, setEditDescricao] = useState("");
  const [editPreco, setEditPreco] = useState("");
  const [editCategoria, setEditCategoria] = useState("");
  const [editDuracaoMinutos, setEditDuracaoMinutos] = useState("");
  const [editTrialDisponivel, setEditTrialDisponivel] = useState(false);
  const [editPublico, setEditPublico] = useState(false);
  const [editSaving, setEditSaving] = useState(false);

  const categoriaObrigatoriaCriar = publico && !categoria.trim();
  const categoriaObrigatoriaEditar = editPublico && !editCategoria.trim();

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

  const resetForm = () => {
    setNome(""); setDescricao(""); setPreco("");
    setCategoria(""); setDuracaoMinutos(""); setTrialDisponivel(false); setPublico(false);
  };

  const handleCriar = async () => {
    if (!nome.trim() || !preco || categoriaObrigatoriaCriar) return;
    setSaving(true);
    try {
      await treinadorApi.criarPacote({
        nome: nome.trim(),
        preco: Number(preco),
        descricao: descricao.trim() || null,
        categoria: categoria.trim() || null,
        duracaoMinutos: duracaoMinutos ? Number(duracaoMinutos) : null,
        trialDisponivel,
        isPublico: publico,
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
    setEditCategoria(p.categoria ?? "");
    setEditDuracaoMinutos(p.duracaoMinutos != null ? String(p.duracaoMinutos) : "");
    setEditTrialDisponivel(p.trialDisponivel ?? false);
    setEditPublico(p.isPublico ?? false);
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
    if (!editTarget || !editNome.trim() || !editPreco || categoriaObrigatoriaEditar) return;
    setEditSaving(true);
    try {
      await treinadorApi.atualizarPacote(editTarget.pacoteId, {
        nome: editNome.trim(),
        preco: Number(editPreco),
        descricao: editDescricao.trim() || null,
        // "" e 0 são as sentinelas de "limpar" do contrato de AtualizarCatalogoPublico —
        // null significa "campo ausente" (não mexer), por isso nunca usado aqui: o form
        // de edição sempre representa o estado completo do pacote.
        categoria: editCategoria.trim(),
        duracaoMinutos: editDuracaoMinutos ? Number(editDuracaoMinutos) : 0,
        trialDisponivel: editTrialDisponivel,
        isPublico: editPublico,
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
      <PageHeader
        title="Pacotes de Treinos"
        action={
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>
            Novo pacote
          </Button>
        }
      />

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
                    <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 0.5 }}>
                      <Typography variant="h6">{p.nome}</Typography>
                      {p.isPublico && <Chip label="Público" size="small" color="primary" variant="outlined" />}
                    </Stack>
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
                    {formatarBRL(p.preco)}/mês
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}

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
            <TextField
              label="Categoria"
              value={categoria}
              onChange={(e) => setCategoria(e.target.value)}
              size="small"
              fullWidth
              error={categoriaObrigatoriaCriar}
              helperText={categoriaObrigatoriaCriar
                ? "Categoria é obrigatória para tornar o pacote público."
                : "Ex: Pilates, Funcional"}
            />
            <TextField
              label="Duração (min)"
              type="number"
              value={duracaoMinutos}
              onChange={(e) => setDuracaoMinutos(e.target.value)}
              size="small"
              fullWidth
              slotProps={{ htmlInput: { min: 1, step: 1 } }}
            />
            <FormControlLabel
              control={<Switch checked={trialDisponivel} onChange={(e) => setTrialDisponivel(e.target.checked)} />}
              label="Aceita aula experimental"
            />
            <FormControlLabel
              control={<Switch checked={publico} onChange={(e) => setPublico(e.target.checked)} />}
              label="Público (visível no catálogo do agente)"
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { setOpen(false); resetForm(); }}>Cancelar</Button>
          <Button
            variant="contained"
            disabled={!nome.trim() || !preco || categoriaObrigatoriaCriar || saving}
            onClick={handleCriar}
          >
            Criar
          </Button>
        </DialogActions>
      </Dialog>

      <ConfirmDialog
        open={!!deleteTarget}
        title="Excluir pacote"
        description={`Tem certeza que deseja excluir ${deleteTarget?.nome ?? ""}? Esta ação não pode ser desfeita.`}
        confirmLabel="Excluir"
        destructive
        loading={deleting}
        onConfirm={handleExcluir}
        onClose={() => setDeleteTarget(null)}
      />

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
            <TextField
              label="Categoria"
              value={editCategoria}
              onChange={(e) => setEditCategoria(e.target.value)}
              size="small"
              fullWidth
              error={categoriaObrigatoriaEditar}
              helperText={categoriaObrigatoriaEditar
                ? "Categoria é obrigatória para tornar o pacote público."
                : "Ex: Pilates, Funcional"}
            />
            <TextField
              label="Duração (min)"
              type="number"
              value={editDuracaoMinutos}
              onChange={(e) => setEditDuracaoMinutos(e.target.value)}
              size="small"
              fullWidth
              slotProps={{ htmlInput: { min: 1, step: 1 } }}
            />
            <FormControlLabel
              control={<Switch checked={editTrialDisponivel} onChange={(e) => setEditTrialDisponivel(e.target.checked)} />}
              label="Aceita aula experimental"
            />
            <FormControlLabel
              control={<Switch checked={editPublico} onChange={(e) => setEditPublico(e.target.checked)} />}
              label="Público (visível no catálogo do agente)"
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditTarget(null)}>Cancelar</Button>
          <Button
            variant="contained"
            disabled={!editNome.trim() || !editPreco || categoriaObrigatoriaEditar || editSaving}
            onClick={handleEditar}
          >
            Salvar
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
