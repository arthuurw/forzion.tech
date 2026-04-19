"use client";
import { useCallback, useEffect, useState } from "react";
import {
  Box, Typography, Tabs, Tab, Card, Table, TableHead, TableRow, TableCell, TableBody,
  TablePagination, Button, Dialog, DialogTitle, DialogContent, DialogActions,
  Stack, TextField, IconButton, Tooltip,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { treinadorApi } from "@/lib/api/treinador";
import type { ExercicioResponse } from "@/types";

export default function ExerciciosTreinadorPage() {
  const [tab, setTab] = useState(0);
  const [exercicios, setExercicios] = useState<ExercicioResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [open, setOpen] = useState(false);
  const [nome, setNome] = useState("");
  const [descricao, setDescricao] = useState("");
  const [grupoMuscular, setGrupoMuscular] = useState("");
  const [saving, setSaving] = useState(false);
  const [copiando, setCopiando] = useState<string | null>(null);

  const isGlobal = tab === 1;

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await treinadorApi.listExercicios({ global: isGlobal, pagina: page + 1, tamanhoPagina: pageSize });
      setExercicios(res.data.items);
      setTotal(res.data.total);
    } catch {
      setError("Erro ao carregar exercícios.");
    } finally {
      setLoading(false);
    }
  }, [isGlobal, page, pageSize]);

  useEffect(() => { load(); }, [load]);

  const handleTabChange = (_: unknown, v: number) => { setTab(v); setPage(0); };

  const resetForm = () => { setNome(""); setDescricao(""); setGrupoMuscular(""); };

  const handleCriar = async () => {
    if (!nome.trim()) return;
    setSaving(true);
    try {
      await treinadorApi.criarExercicio({
        nome: nome.trim(),
        descricao: descricao.trim() || null,
        grupoMuscular: grupoMuscular.trim() || null,
      });
      setSuccess(`Exercício "${nome}" criado.`);
      setOpen(false);
      resetForm();
      load();
    } catch {
      setError("Erro ao criar exercício.");
    } finally {
      setSaving(false);
    }
  };

  const handleCopiar = async (ex: ExercicioResponse) => {
    setCopiando(ex.exercicioId);
    try {
      await treinadorApi.copiarExercicioGlobal(ex.exercicioId);
      setSuccess(`"${ex.nome}" copiado para sua biblioteca.`);
    } catch {
      setError("Erro ao copiar exercício.");
    } finally {
      setCopiando(null);
    }
  };

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
        <Typography variant="h5" sx={{ fontWeight: 700 }}>Exercícios</Typography>
        {!isGlobal && (
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>
            Novo exercício
          </Button>
        )}
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Tabs value={tab} onChange={handleTabChange} sx={{ mb: 2 }}>
        <Tab label="Meus exercícios" />
        <Tab label="Globais" />
      </Tabs>

      <Card variant="outlined">
        {loading ? (
          <LoadingSpinner />
        ) : exercicios.length === 0 ? (
          <EmptyState
            message={isGlobal ? "Nenhum exercício global disponível." : "Nenhum exercício criado ainda."}
            actionLabel={!isGlobal ? "Criar exercício" : undefined}
            onAction={!isGlobal ? () => setOpen(true) : undefined}
          />
        ) : (
          <>
            <Box sx={{ overflowX: "auto" }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 600 }}>Nome</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Grupo muscular</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Descrição</TableCell>
                    {isGlobal && <TableCell align="right" sx={{ fontWeight: 600 }}>Ações</TableCell>}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {exercicios.map((ex) => (
                    <TableRow key={ex.exercicioId} hover>
                      <TableCell sx={{ fontWeight: 500 }}>{ex.nome}</TableCell>
                      <TableCell>{ex.grupoMuscular ?? "—"}</TableCell>
                      <TableCell>
                        <Typography
                          variant="caption"
                          color="text.secondary"
                          sx={{
                            display: "block",
                            maxWidth: 280,
                            overflow: "hidden",
                            textOverflow: "ellipsis",
                            whiteSpace: "nowrap",
                          }}
                        >
                          {ex.descricao ?? "—"}
                        </Typography>
                      </TableCell>
                      {isGlobal && (
                        <TableCell align="right">
                          <Tooltip title="Copiar para minha biblioteca">
                            <span>
                              <IconButton
                                size="small"
                                disabled={copiando === ex.exercicioId}
                                onClick={() => handleCopiar(ex)}
                              >
                                <ContentCopyIcon fontSize="small" />
                              </IconButton>
                            </span>
                          </Tooltip>
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
            <TablePagination
              component="div"
              count={total}
              page={page}
              rowsPerPage={pageSize}
              onPageChange={(_, p) => setPage(p)}
              onRowsPerPageChange={(e) => { setPageSize(+e.target.value); setPage(0); }}
              rowsPerPageOptions={[5, 10, 25]}
              labelRowsPerPage="Por página:"
              labelDisplayedRows={({ from, to, count }) => `${from}–${to} de ${count}`}
            />
          </>
        )}
      </Card>

      <Dialog open={open} onClose={() => { setOpen(false); resetForm(); }} maxWidth="xs" fullWidth>
        <DialogTitle>Novo exercício</DialogTitle>
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
              label="Grupo muscular"
              value={grupoMuscular}
              onChange={(e) => setGrupoMuscular(e.target.value)}
              size="small"
              fullWidth
            />
            <TextField
              label="Descrição"
              value={descricao}
              onChange={(e) => setDescricao(e.target.value)}
              size="small"
              fullWidth
              multiline
              rows={2}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => { setOpen(false); resetForm(); }}>Cancelar</Button>
          <Button variant="contained" disabled={!nome.trim() || saving} onClick={handleCriar}>
            Criar
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
