"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  Box, Typography, Card, CardContent, Stack, Button,
  Table, TableHead, TableRow, TableCell, TableBody,
  Dialog, DialogTitle, DialogContent, DialogActions,
  Autocomplete, TextField, IconButton,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import AddIcon from "@mui/icons-material/Add";
import StatusChip from "@/components/ui/StatusChip";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { treinadorApi } from "@/lib/api/treinador";
import type { AlunoResponse, TreinoAlunoResponse, TreinoResponse } from "@/types";

export default function DetalheAlunoPage() {
  const { alunoId } = useParams<{ alunoId: string }>();
  const router = useRouter();
  const [aluno, setAluno] = useState<AlunoResponse | null>(null);
  const [fichas, setFichas] = useState<TreinoAlunoResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [vincularOpen, setVincularOpen] = useState(false);
  const [todasFichas, setTodasFichas] = useState<TreinoResponse[]>([]);
  const [selectedFicha, setSelectedFicha] = useState<TreinoResponse | null>(null);
  const [loadingVincular, setLoadingVincular] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [alunoRes, fichasRes] = await Promise.all([
        treinadorApi.getAluno(alunoId),
        treinadorApi.getFichasDoAluno(alunoId),
      ]);
      setAluno(alunoRes.data);
      setFichas(fichasRes.data);
    } catch {
      setError("Erro ao carregar dados do aluno.");
    } finally {
      setLoading(false);
    }
  }, [alunoId]);

  useEffect(() => { load(); }, [load]);

  const openVincular = async () => {
    setVincularOpen(true);
    setSelectedFicha(null);
    if (todasFichas.length === 0) {
      try {
        const res = await treinadorApi.listFichas({ tamanhoPagina: 100 });
        setTodasFichas(res.data.items);
      } catch {
        setError("Erro ao carregar fichas.");
      }
    }
  };

  const handleVincular = async () => {
    if (!selectedFicha) return;
    setLoadingVincular(true);
    try {
      await treinadorApi.vincularFichaAoAluno(alunoId, selectedFicha.treinoId);
      setSuccess(`Ficha "${selectedFicha.nome}" vinculada com sucesso.`);
      setVincularOpen(false);
      load();
    } catch {
      setError("Erro ao vincular ficha.");
    } finally {
      setLoadingVincular(false);
    }
  };

  if (loading) return <LoadingSpinner />;

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 3 }}>
        <IconButton onClick={() => router.push("/treinador/alunos")} size="small">
          <ArrowBackIcon />
        </IconButton>
        <Box sx={{ flex: 1 }}>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>{aluno?.nome ?? "Aluno"}</Typography>
        </Box>
        {aluno && <StatusChip status={aluno.status} />}
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      {aluno && (
        <Card variant="outlined" sx={{ mb: 3 }}>
          <CardContent>
            <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>
              Dados do aluno
            </Typography>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ flexWrap: "wrap" }}>
              {aluno.email && (
                <Typography variant="body2">
                  <strong>E-mail:</strong> {aluno.email}
                </Typography>
              )}
              {aluno.telefone && (
                <Typography variant="body2">
                  <strong>Telefone:</strong> {aluno.telefone}
                </Typography>
              )}
              <Typography variant="body2">
                <strong>Cadastro:</strong> {new Date(aluno.createdAt).toLocaleDateString("pt-BR")}
              </Typography>
            </Stack>
          </CardContent>
        </Card>
      )}

      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
        <Typography variant="h6" sx={{ fontWeight: 600 }}>
          Fichas vinculadas ({fichas.length})
        </Typography>
        <Button variant="outlined" size="small" startIcon={<AddIcon />} onClick={openVincular}>
          Vincular ficha
        </Button>
      </Box>

      <Card variant="outlined">
        {fichas.length === 0 ? (
          <EmptyState
            message="Nenhuma ficha vinculada a este aluno."
            actionLabel="Vincular ficha"
            onAction={openVincular}
          />
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 600 }}>Ficha</TableCell>
                <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {fichas.map((f) => (
                <TableRow key={f.treinoAlunoId} hover>
                  <TableCell sx={{ fontWeight: 500 }}>{f.nomeTreino}</TableCell>
                  <TableCell><StatusChip status={f.status} /></TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Card>

      <Dialog open={vincularOpen} onClose={() => setVincularOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Vincular ficha a {aluno?.nome}</DialogTitle>
        <DialogContent sx={{ pt: 2 }}>
          <Autocomplete
            options={todasFichas}
            getOptionLabel={(f) => `${f.nome} — ${f.objetivo}`}
            value={selectedFicha}
            onChange={(_, v) => setSelectedFicha(v)}
            renderInput={(params) => <TextField {...params} label="Ficha" size="small" />}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setVincularOpen(false)}>Cancelar</Button>
          <Button
            variant="contained"
            disabled={!selectedFicha || loadingVincular}
            onClick={handleVincular}
          >
            Vincular
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
