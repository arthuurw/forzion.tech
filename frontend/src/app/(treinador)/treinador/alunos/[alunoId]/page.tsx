"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  Box, Typography, Card, CardContent, Stack, Button,
  Table, TableHead, TableRow, TableCell, TableBody,
  Dialog, DialogTitle, DialogContent, DialogActions,
  Autocomplete, TextField, IconButton, Chip,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import AddIcon from "@mui/icons-material/Add";
import StatusChip from "@/components/ui/StatusChip";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { treinadorApi } from "@/lib/api/treinador";
import type {
  AlunoResponse, TreinoAlunoResponse, TreinoResponse,
  FinalidadeTreino, NivelCondicionamento, TempoDisponivel,
} from "@/types";
import ProgressaoAluno from "@/components/treinador/ProgressaoAluno";

const FINALIDADE_LABEL: Record<FinalidadeTreino, string> = {
  Hipertrofia: "Hipertrofia",
  Emagrecimento: "Emagrecimento",
  CondicionamentoFisico: "Condicionamento Físico",
  Saude: "Saúde Geral",
  PerformanceEsportiva: "Performance Esportiva",
  Reabilitacao: "Reabilitação",
  Outro: "Outro",
};

const NIVEL_LABEL: Record<NivelCondicionamento, string> = {
  Sedentario: "Sedentário",
  Iniciante: "Iniciante",
  Intermediario: "Intermediário",
  Avancado: "Avançado",
};

const TEMPO_LABEL: Record<TempoDisponivel, string> = {
  TrintaMinutos: "30 min",
  QuarentaCincoMinutos: "45 min",
  UmaHora: "1 hora",
  UmaHoraETrinta: "1h30",
  DuasHoras: "2 horas ou mais",
};

function InfoLinha({ label, value }: { label: string; value: string | number }) {
  return (
    <Typography variant="body2">
      <strong>{label}:</strong> {value}
    </Typography>
  );
}

export default function DetalheAlunoPage() {
  const { alunoId } = useParams<{ alunoId: string }>();
  const router = useRouter();
  const [aluno, setAluno] = useState<AlunoResponse | null>(null);
  const [fichas, setFichas] = useState<TreinoAlunoResponse[]>([]);
  const [pacoteNome, setPacoteNome] = useState<string | null>(null);
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
      const [alunoRes, fichasRes, vinculosRes, pacotesRes] = await Promise.all([
        treinadorApi.getAluno(alunoId),
        treinadorApi.getFichasDoAluno(alunoId),
        treinadorApi.listVinculos({ tamanhoPagina: 200 }),
        treinadorApi.listPacotes(),
      ]);
      setAluno(alunoRes.data);
      setFichas(fichasRes.data);

      const vinculo = vinculosRes.data.items.find((v) => v.alunoId === alunoId);
      if (vinculo?.pacoteAlunoId) {
        const pacote = pacotesRes.data.find((p) => p.pacoteId === vinculo.pacoteAlunoId);
        setPacoteNome(pacote?.nome ?? null);
      }
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

  const temPerfilTreino = aluno && (
    aluno.finalidade || aluno.nivelCondicionamento || aluno.diasDisponiveis ||
    aluno.focoTreino || aluno.limitacoesFisicas || aluno.doencas || aluno.observacoesAdicionais
  );

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
        <Card variant="outlined" sx={{ mb: 2 }}>
          <CardContent>
            <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>
              Dados do aluno
            </Typography>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ flexWrap: "wrap" }}>
              {aluno.email && <InfoLinha label="E-mail" value={aluno.email} />}
              {aluno.telefone && <InfoLinha label="Celular" value={aluno.telefone} />}
              <InfoLinha label="Cadastro" value={new Date(aluno.createdAt).toLocaleDateString("pt-BR")} />
              {pacoteNome && (
                <Typography variant="body2">
                  <strong>Pacote:</strong>{" "}
                  <Chip label={pacoteNome} size="small" color="primary" sx={{ ml: 0.5, fontWeight: 600 }} />
                </Typography>
              )}
            </Stack>
          </CardContent>
        </Card>
      )}

      {temPerfilTreino && aluno && (
        <Card variant="outlined" sx={{ mb: 3 }}>
          <CardContent>
            <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1.5 }}>
              Perfil de treino
            </Typography>
            <Stack spacing={0.75}>
              {aluno.finalidade && (
                <InfoLinha label="Finalidade" value={FINALIDADE_LABEL[aluno.finalidade]} />
              )}
              {aluno.nivelCondicionamento && (
                <InfoLinha label="Nível" value={NIVEL_LABEL[aluno.nivelCondicionamento]} />
              )}
              {(aluno.diasDisponiveis || aluno.tempoDisponivelMinutos) && (
                <Typography variant="body2">
                  <strong>Disponibilidade:</strong>{" "}
                  {aluno.diasDisponiveis ? `${aluno.diasDisponiveis} dias/semana` : ""}
                  {aluno.diasDisponiveis && aluno.tempoDisponivelMinutos ? " · " : ""}
                  {aluno.tempoDisponivelMinutos ? TEMPO_LABEL[aluno.tempoDisponivelMinutos] + "/dia" : ""}
                </Typography>
              )}
              {aluno.focoTreino && <InfoLinha label="Foco" value={aluno.focoTreino} />}
              {aluno.limitacoesFisicas && (
                <InfoLinha label="Limitações físicas" value={aluno.limitacoesFisicas} />
              )}
              {aluno.doencas && <InfoLinha label="Doenças / condições" value={aluno.doencas} />}
              {aluno.observacoesAdicionais && (
                <InfoLinha label="Observações" value={aluno.observacoesAdicionais} />
              )}
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

      <ProgressaoAluno alunoId={alunoId} />

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
