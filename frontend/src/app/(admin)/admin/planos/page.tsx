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
import { adminApi } from "@/lib/api/admin";
import type { PlanoTreinadorResponse } from "@/types";

export default function PlanosAdminPage() {
  const [planos, setPlanos] = useState<PlanoTreinadorResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [open, setOpen] = useState(false);
  const [nome, setNome] = useState("");
  const [maxAlunos, setMaxAlunos] = useState("");
  const [preco, setPreco] = useState("");
  const [saving, setSaving] = useState(false);

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
    if (!nome.trim() || !maxAlunos || !preco) return;
    setSaving(true);
    try {
      await adminApi.criarPlano(nome.trim(), Number(maxAlunos), Number(preco));
      setSuccess(`Plano "${nome}" criado.`);
      setOpen(false);
      setNome("");
      setMaxAlunos("");
      setPreco("");
      load();
    } catch {
      setError("Erro ao criar plano.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700 }}>Planos</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>
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
          onAction={() => setOpen(true)}
        />
      ) : (
        <Grid container spacing={2}>
          {planos.map((p) => (
            <Grid key={p.planoId} size={{ xs: 12, sm: 6, md: 4 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" sx={{ fontWeight: 700, mb: 0.5 }}>{p.nome}</Typography>
                  <Typography variant="body2" color="text.secondary">
                    Até {p.maxAlunos} alunos
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    R$ {Number(p.preco).toFixed(2)}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}

      <Dialog open={open} onClose={() => setOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Novo plano</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField
              label="Nome"
              value={nome}
              onChange={(e) => setNome(e.target.value)}
              size="small"
              fullWidth
              required
            />
            <TextField
              label="Máximo de alunos"
              type="number"
              value={maxAlunos}
              onChange={(e) => setMaxAlunos(e.target.value)}
              size="small"
              fullWidth
              required
              slotProps={{ htmlInput: { min: 1 } }}
            />
            <TextField
              label="Preço"
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
          <Button onClick={() => setOpen(false)}>Cancelar</Button>
          <Button
            variant="contained"
            disabled={!nome.trim() || !maxAlunos || !preco || saving}
            onClick={handleCriar}
          >
            Criar
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
