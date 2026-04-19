"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, Card, CardContent, Grid, Button,
  Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Stack,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { treinadorApi } from "@/lib/api/treinador";
import type { PacoteAlunoResponse } from "@/types";

export default function PacotesTreinadorPage() {
  const [pacotes, setPacotes] = useState<PacoteAlunoResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [open, setOpen] = useState(false);
  const [nome, setNome] = useState("");
  const [maxFichas, setMaxFichas] = useState("");
  const [preco, setPreco] = useState("");
  const [saving, setSaving] = useState(false);

  const load = async () => {
    setLoading(true);
    try {
      const res = await treinadorApi.listPacotes();
      setPacotes(res.data);
    } catch {
      setError("Erro ao carregar pacotes.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const resetForm = () => { setNome(""); setMaxFichas(""); setPreco(""); };

  const handleCriar = async () => {
    if (!nome.trim() || !maxFichas || !preco) return;
    setSaving(true);
    try {
      await treinadorApi.criarPacote({ nome: nome.trim(), maxFichas: Number(maxFichas), preco: Number(preco) });
      setSuccess(`Pacote "${nome}" criado.`);
      setOpen(false);
      resetForm();
      load();
    } catch {
      setError("Erro ao criar pacote.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700 }}>Pacotes de Fichas</Typography>
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
          message="Nenhum pacote cadastrado. Crie pacotes para oferecer aos seus alunos."
          actionLabel="Criar primeiro pacote"
          onAction={() => setOpen(true)}
        />
      ) : (
        <Grid container spacing={2}>
          {pacotes.map((p) => (
            <Grid key={p.pacoteId} size={{ xs: 12, sm: 6, md: 4 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" sx={{ fontWeight: 700, mb: 0.5 }}>{p.nome}</Typography>
                  <Typography variant="body2" color="text.secondary">
                    Até {p.maxFichas} {p.maxFichas === 1 ? "ficha" : "fichas"}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    R$ {p.preco.toFixed(2)}/mês
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}

      <Dialog open={open} onClose={() => { setOpen(false); resetForm(); }} maxWidth="xs" fullWidth>
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
              label="Máximo de fichas"
              type="number"
              value={maxFichas}
              onChange={(e) => setMaxFichas(e.target.value)}
              size="small"
              fullWidth
              required
              slotProps={{ htmlInput: { min: 1 } }}
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
            disabled={!nome.trim() || !maxFichas || !preco || saving}
            onClick={handleCriar}
          >
            Criar
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
