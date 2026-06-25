"use client";
import { useCallback, useEffect, useState } from "react";
import dynamic from "next/dynamic";
import { useParams, useRouter } from "next/navigation";
import {
  Box, Typography, Card, CardContent, Stack, Button,
  Dialog, DialogTitle, DialogContent, DialogActions,
  Autocomplete, TextField, IconButton,
} from "@mui/material";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import AddIcon from "@mui/icons-material/Add";
import StatusChip from "@/components/ui/StatusChip";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { treinadorApi } from "@/lib/api/treinador";
import type {
  AlunoResponse, TreinoAlunoResponse, TreinoResponse, PacoteResponse,
  FinalidadeTreino, NivelCondicionamento, TempoDisponivel,
} from "@/types";
import InfoLine from "@/components/ui/InfoLine";
import { OBJETIVO_LABEL, FINALIDADE_LABEL, NIVEL_LABEL, TEMPO_LABEL } from "@/lib/constants/labels";
import { MAX_PAGE_SIZE } from "@/lib/constants/pagination";
import { formatarBRL, formatarTelefone } from "@/lib/utils/formatting";
import { extractApiError } from "@/lib/api/extractApiError";

const ProgressaoAluno = dynamic(
  () => import("@/components/treinador/ProgressaoAluno"),
  { ssr: false, loading: () => <LoadingSpinner /> },
);

const FICHAS_COLS: Column[] = [
  { label: "Ficha", mobileRole: "primary" },
  { label: "Status", mobileRole: "secondary" },
];

export default function DetalheAlunoPage() {
  const { alunoId } = useParams<{ alunoId: string }>();
  const router = useRouter();
  const [aluno, setAluno] = useState<AlunoResponse | null>(null);
  const [fichas, setFichas] = useState<TreinoAlunoResponse[]>([]);
  const [pacote, setPacote] = useState<PacoteResponse | null>(null);
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
        treinadorApi.listVinculos({ tamanhoPagina: MAX_PAGE_SIZE }),
        treinadorApi.listPacotes(),
      ]);
      setAluno(alunoRes.data);
      setFichas(fichasRes.data);

      const vinculo = vinculosRes.data.items.find((v) => v.alunoId === alunoId);
      if (vinculo?.pacoteId) {
        setPacote(pacotesRes.data.find((p) => p.pacoteId === vinculo.pacoteId) ?? null);
      }
    } catch (err) {
      setError(extractApiError(err, "Erro ao carregar dados do aluno."));
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
      } catch (err) {
        setError(extractApiError(err, "Erro ao carregar fichas."));
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
    } catch (err) {
      setError(extractApiError(err, "Erro ao vincular ficha."));
    } finally {
      setLoadingVincular(false);
    }
  };

  const temPerfilTreino = aluno && (
    aluno.finalidade || aluno.nivelCondicionamento || aluno.diasDisponiveis || aluno.tempoDisponivelMinutos ||
    aluno.focoTreino || aluno.limitacoesFisicas || aluno.doencas || aluno.observacoesAdicionais
  );

  if (loading) return <LoadingSpinner />;

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 3 }}>
        <IconButton onClick={() => router.push("/treinador/alunos")} aria-label="Voltar">
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
        <>
          <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>Dados do aluno</Typography>
          <Card variant="outlined" sx={{ mb: 3 }}>
            <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
              <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ flexWrap: "wrap" }}>
                {aluno.email && <InfoLine label="E-mail" value={aluno.email} />}
                {aluno.telefone && <InfoLine label="Celular" value={formatarTelefone(aluno.telefone)} />}
                <InfoLine label="Cadastro" value={new Date(aluno.createdAt).toLocaleDateString("pt-BR")} />
                {aluno.updatedAt && <InfoLine label="Atualizado" value={new Date(aluno.updatedAt).toLocaleDateString("pt-BR")} />}
                {pacote && (
                  <Box sx={{ display: "flex", alignItems: "center", gap: 0.75, flexWrap: "wrap" }}>
                    <Typography variant="body2" component="span"><strong>Pacote:</strong></Typography>
                    <Box
                      sx={{
                        display: "inline-flex", alignItems: "center", gap: 0.75, flexWrap: "wrap",
                        bgcolor: "primary.main", color: "primary.contrastText", borderRadius: 2, px: 1, py: 0.25,
                      }}
                    >
                      <Typography variant="body2" component="span">
                        <strong>{pacote.nome}</strong> - {formatarBRL(pacote.preco)}
                        {pacote.descricao ? ` - ${pacote.descricao}` : ""}
                      </Typography>
                    </Box>
                  </Box>
                )}
              </Stack>
            </CardContent>
          </Card>
        </>
      )}

      {temPerfilTreino && aluno && (
        <>
          <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>Perfil de treino</Typography>
          <Card variant="outlined" sx={{ mb: 3 }}>
            <CardContent>
            <Stack spacing={0.75}>
              {aluno.finalidade && (
                <InfoLine label="Finalidade" value={FINALIDADE_LABEL[aluno.finalidade]} />
              )}
              {aluno.nivelCondicionamento && (
                <InfoLine label="Nível" value={NIVEL_LABEL[aluno.nivelCondicionamento]} />
              )}
              {(aluno.diasDisponiveis || aluno.tempoDisponivelMinutos) && (
                <Typography variant="body2">
                  <strong>Disponibilidade:</strong>{" "}
                  {aluno.diasDisponiveis ? `${aluno.diasDisponiveis} dias/semana` : ""}
                  {aluno.diasDisponiveis && aluno.tempoDisponivelMinutos ? " · " : ""}
                  {aluno.tempoDisponivelMinutos ? TEMPO_LABEL[aluno.tempoDisponivelMinutos] + "/dia" : ""}
                </Typography>
              )}
              {aluno.focoTreino && <InfoLine label="Foco" value={aluno.focoTreino} />}
              {aluno.limitacoesFisicas && (
                <InfoLine label="Limitações físicas" value={aluno.limitacoesFisicas} />
              )}
              {aluno.doencas && <InfoLine label="Doenças / condições" value={aluno.doencas} />}
              {aluno.observacoesAdicionais && (
                <InfoLine label="Observações" value={aluno.observacoesAdicionais} />
              )}
            </Stack>
            </CardContent>
          </Card>
        </>
      )}

      {aluno?.status === "AguardandoAprovacao" ? (
        <Card variant="outlined" sx={{ p: 2 }}>
          <Typography variant="body2" color="text.secondary">
            Vínculo aguardando aprovação. Fichas e histórico disponíveis após aprovação.
          </Typography>
        </Card>
      ) : (
        <>
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
              <ResponsiveTable
                columns={FICHAS_COLS}
                rows={fichas}
                rowKey={(f) => f.treinoAlunoId}
                renderCell={(f, i) => {
                  if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{f.nomeTreino}</Typography>;
                  return <StatusChip status={f.status} />;
                }}
              />
            )}
          </Card>

          <ProgressaoAluno alunoId={alunoId} />
        </>
      )}

      <Dialog open={vincularOpen} onClose={() => setVincularOpen(false)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Vincular ficha a {aluno?.nome}</DialogTitle>
        <DialogContent sx={{ pt: 2 }}>
          <Autocomplete
            options={todasFichas}
            getOptionLabel={(f) => `${f.nome} — ${OBJETIVO_LABEL[f.objetivo] ?? f.objetivo}`}
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
