"use client";
import { useCallback, useEffect, useState } from "react";
import {
  Box, Typography, Card, Button, Dialog, DialogTitle, DialogContent, DialogActions,
  Stack, TextField, Select, MenuItem, FormControl, InputLabel, IconButton, Tooltip, Autocomplete,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import { useRouter } from "next/navigation";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import { treinadorApi } from "@/lib/api/treinador";
import type { TreinoResponse, ObjetivoTreino, AlunoResponse } from "@/types";

const OBJETIVOS: ObjetivoTreino[] = [
  "Hipertrofia", "Emagrecimento", "Resistencia", "Forca", "Flexibilidade", "Condicionamento",
];

const COLUMNS: Column[] = [
  { label: "Nome" },
  { label: "Objetivo" },
  { label: "Exercícios" },
  { label: "Criado em" },
  { label: "Ações", align: "right" },
];

export default function TreinosTreinadorPage() {
  const router = useRouter();
  const [fichas, setFichas] = useState<TreinoResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [open, setOpen] = useState(false);
  const [nome, setNome] = useState("");
  const [objetivo, setObjetivo] = useState<ObjetivoTreino>("Hipertrofia");
  const [saving, setSaving] = useState(false);
  const [alunos, setAlunos] = useState<AlunoResponse[]>([]);
  const [selectedAluno, setSelectedAluno] = useState<AlunoResponse | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await treinadorApi.listFichas({ pagina: page + 1, tamanhoPagina: pageSize });
      setFichas(res.data.items);
      setTotal(res.data.total);
    } catch {
      setError("Erro ao carregar fichas.");
    } finally {
      setLoading(false);
    }
  }, [page, pageSize]);

  useEffect(() => { load(); }, [load]);

  const resetForm = () => { setNome(""); setObjetivo("Hipertrofia"); setSelectedAluno(null); };

  const openDialog = async () => {
    setOpen(true);
    if (alunos.length === 0) {
      try {
        const res = await treinadorApi.listAlunos({ status: "Ativo", tamanhoPagina: 200 });
        setAlunos(res.data.items);
      } catch {
        setError("Erro ao carregar alunos ativos.");
      }
    }
  };

  const handleCriar = async () => {
    if (!nome.trim() || !selectedAluno) return;
    setSaving(true);
    try {
      const res = await treinadorApi.criarFicha({ alunoId: selectedAluno.alunoId, nome: nome.trim(), objetivo });
      setOpen(false);
      resetForm();
      router.push(`/treinador/treinos/${res.data.treinoId}`);
    } catch {
      setError("Erro ao criar ficha.");
      setSaving(false);
    }
  };

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 700 }}>Fichas de Treino</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openDialog}>
          Nova ficha
        </Button>
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <Card variant="outlined">
        {loading ? (
          <LoadingSpinner />
        ) : fichas.length === 0 ? (
          <EmptyState
            message="Nenhuma ficha cadastrada. Crie a primeira para começar a prescrever treinos."
            actionLabel="Criar primeira ficha"
            onAction={openDialog}
          />
        ) : (
          <ResponsiveTable
            columns={COLUMNS}
            rows={fichas}
            rowKey={(f) => f.treinoId}
            onRowClick={(f) => router.push(`/treinador/treinos/${f.treinoId}`)}
            pagination={{
              count: total,
              page,
              rowsPerPage: pageSize,
              onPageChange: setPage,
              onRowsPerPageChange: (size) => { setPageSize(size); setPage(0); },
            }}
            renderCell={(f, i) => {
              if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{f.nome}</Typography>;
              if (i === 1) return f.objetivo;
              if (i === 2) return f.exercicios.length;
              if (i === 3) return (
                <Typography variant="caption">
                  {new Date(f.createdAt).toLocaleDateString("pt-BR")}
                </Typography>
              );
              return (
                <Tooltip title="Abrir ficha">
                  <IconButton
                    size="small"
                    onClick={() => router.push(`/treinador/treinos/${f.treinoId}`)}
                  >
                    <OpenInNewIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              );
            }}
          />
        )}
      </Card>

      <Dialog open={open} onClose={() => { setOpen(false); resetForm(); }} maxWidth="xs" fullWidth>
        <DialogTitle>Nova ficha de treino</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <Autocomplete
              options={alunos}
              getOptionLabel={(a) => a.nome}
              value={selectedAluno}
              onChange={(_, v) => setSelectedAluno(v)}
              noOptionsText="Nenhum aluno ativo na carteira"
              renderInput={(params) => (
                <TextField
                  {...params}
                  label="Aluno"
                  size="small"
                  required
                  helperText="A ficha será automaticamente vinculada ao aluno selecionado."
                />
              )}
            />
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
                  <MenuItem key={o} value={o}>{o}</MenuItem>
                ))}
              </Select>
            </FormControl>
            {alunos.length === 0 && (
              <Typography variant="caption" color="text.secondary">
                Para criar fichas vinculadas, é necessário ter ao menos um aluno ativo na sua carteira.
              </Typography>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { setOpen(false); resetForm(); }}>Cancelar</Button>
          <Button variant="contained" disabled={!nome.trim() || !selectedAluno || saving} onClick={handleCriar}>
            Criar e abrir
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
