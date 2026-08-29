"use client";
import { useEffect, useState, type ReactNode } from "react";
import {
  Box, Card, CardContent, Button, Grid, TextField, Stack, Typography, IconButton, Chip, MenuItem, Divider,
  Tabs, Tab,
} from "@mui/material";
import DeleteIcon from "@mui/icons-material/Delete";
import AddIcon from "@mui/icons-material/Add";
import { DatePicker } from "@mui/x-date-pickers/DatePicker";
import dayjs, { type Dayjs } from "dayjs";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import PageHeader from "@/components/ui/PageHeader";
import { extractApiError } from "@/lib/api/extractApiError";
import { agendaApi } from "@/lib/api/agenda";
import type { BloqueioAgendaResponse } from "@/types";
import { TIPO_BLOQUEIO_LABEL, DIA_SEMANA_LABEL } from "@/lib/constants/labels";
import SolicitacoesTab from "./_components/SolicitacoesTab";

const DATE_FIELD_SLOTS = {
  textField: { size: "small" as const, fullWidth: true },
};

const DIAS_SEMANA_OPTIONS = Object.entries(DIA_SEMANA_LABEL).map(([value, label]) => ({
  value: Number(value),
  label,
}));

// Wiring id/aria-controls/aria-labelledby entre Tab e painel: WAI-ARIA Tabs Pattern
// (verificado via context7/MUI docs — "Each tab must be linked to its corresponding
// tabpanel using id, aria-controls, and aria-labelledby").
function a11yProps(index: number) {
  return { id: `agenda-tab-${index}`, "aria-controls": `agenda-tabpanel-${index}` };
}

function AgendaTabPanel({ children, value, index }: { children: ReactNode; value: number; index: number }) {
  if (value !== index) return null;
  return (
    <div role="tabpanel" id={`agenda-tabpanel-${index}`} aria-labelledby={`agenda-tab-${index}`} tabIndex={0}>
      {children}
    </div>
  );
}

function formatarIntervaloPontual(b: BloqueioAgendaResponse): string {
  if (!b.inicioUtc || !b.fimUtc) return "—";
  const inicio = new Date(b.inicioUtc);
  const fim = new Date(b.fimUtc);
  const data = inicio.toLocaleDateString("pt-BR", { timeZone: "UTC" });
  const horaInicio = inicio.toLocaleTimeString("pt-BR", { timeZone: "UTC", hour: "2-digit", minute: "2-digit" });
  const horaFim = fim.toLocaleTimeString("pt-BR", { timeZone: "UTC", hour: "2-digit", minute: "2-digit" });
  return `${data} ${horaInicio} — ${horaFim}`;
}

export default function AgendaTreinadorPage() {
  const [tab, setTab] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [bloqueios, setBloqueios] = useState<BloqueioAgendaResponse[]>([]);

  const [pontualData, setPontualData] = useState<Dayjs | null>(null);
  const [pontualHoraInicio, setPontualHoraInicio] = useState("");
  const [pontualHoraFim, setPontualHoraFim] = useState("");
  const [pontualMotivo, setPontualMotivo] = useState("");
  const [salvandoPontual, setSalvandoPontual] = useState(false);

  const [recorrenteDiaSemana, setRecorrenteDiaSemana] = useState(1);
  const [recorrenteHoraInicio, setRecorrenteHoraInicio] = useState("");
  const [recorrenteHoraFim, setRecorrenteHoraFim] = useState("");
  const [recorrenteMotivo, setRecorrenteMotivo] = useState("");
  const [recorrenteError, setRecorrenteError] = useState("");
  const [salvandoRecorrente, setSalvandoRecorrente] = useState(false);

  const [antecedenciaMinimaHoras, setAntecedenciaMinimaHoras] = useState(2);
  const [horizonteDias, setHorizonteDias] = useState(60);
  const [salvandoPolitica, setSalvandoPolitica] = useState(false);

  useEffect(() => {
    Promise.all([agendaApi.listarBloqueios(), agendaApi.obterPolitica()])
      .then(([bloqueiosRes, politicaRes]) => {
        setBloqueios(bloqueiosRes.data);
        setAntecedenciaMinimaHoras(politicaRes.data.antecedenciaMinimaHoras);
        setHorizonteDias(politicaRes.data.horizonteDias);
      })
      .catch(() => setError("Não foi possível carregar a agenda."))
      .finally(() => setLoading(false));
  }, []);

  const pontuais = bloqueios.filter((b) => b.tipo === "Pontual");
  const recorrentes = bloqueios.filter((b) => b.tipo === "RecorrenteSemanal");

  const handleAdicionarPontual = async () => {
    if (!pontualData || !pontualHoraInicio || !pontualHoraFim) return;
    const dia = pontualData.format("YYYY-MM-DD");
    setSalvandoPontual(true);
    try {
      const { data } = await agendaApi.criarBloqueio({
        tipo: "Pontual",
        inicioUtc: dayjs(`${dia}T${pontualHoraInicio}`).toISOString(),
        fimUtc: dayjs(`${dia}T${pontualHoraFim}`).toISOString(),
        motivo: pontualMotivo.trim() || null,
      });
      setBloqueios((prev) => [...prev, data]);
      setPontualData(null);
      setPontualHoraInicio("");
      setPontualHoraFim("");
      setPontualMotivo("");
    } catch (err) {
      setError(extractApiError(err, "Não foi possível criar o bloqueio."));
    } finally {
      setSalvandoPontual(false);
    }
  };

  const handleAdicionarRecorrente = async () => {
    setRecorrenteError("");
    if (!recorrenteHoraInicio || !recorrenteHoraFim) return;
    if (recorrenteHoraInicio >= recorrenteHoraFim) {
      setRecorrenteError("O horário de início deve ser antes do horário de fim.");
      return;
    }
    setSalvandoRecorrente(true);
    try {
      const { data } = await agendaApi.criarBloqueio({
        tipo: "RecorrenteSemanal",
        diaSemana: recorrenteDiaSemana,
        horaInicio: recorrenteHoraInicio,
        horaFim: recorrenteHoraFim,
        motivo: recorrenteMotivo.trim() || null,
      });
      setBloqueios((prev) => [...prev, data]);
      setRecorrenteHoraInicio("");
      setRecorrenteHoraFim("");
      setRecorrenteMotivo("");
    } catch (err) {
      setError(extractApiError(err, "Não foi possível criar o bloqueio."));
    } finally {
      setSalvandoRecorrente(false);
    }
  };

  const handleApagarBloqueio = async (id: string) => {
    setError("");
    try {
      await agendaApi.apagarBloqueio(id);
      setBloqueios((prev) => prev.filter((b) => b.id !== id));
    } catch (err) {
      setError(extractApiError(err, "Não foi possível apagar o bloqueio."));
    }
  };

  const handleSalvarPolitica = async () => {
    setError("");
    setSuccess("");
    setSalvandoPolitica(true);
    try {
      const { data } = await agendaApi.atualizarPolitica({ antecedenciaMinimaHoras, horizonteDias });
      setAntecedenciaMinimaHoras(data.antecedenciaMinimaHoras);
      setHorizonteDias(data.horizonteDias);
      setSuccess("Política de agenda salva.");
    } catch (err) {
      setError(extractApiError(err, "Não foi possível salvar a política de agenda."));
    } finally {
      setSalvandoPolitica(false);
    }
  };

  if (loading) return <LoadingSpinner />;

  return (
    <Box sx={{ maxWidth: { xs: "100%", md: 720 } }}>
      <PageHeader
        title="Agenda"
        subtitle="Bloqueios e política de disponibilidade oferecida ao agente conversacional."
      />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Tabs value={tab} onChange={(_, v) => setTab(v)} aria-label="Seções da agenda" sx={{ mb: 2 }}>
        <Tab label="Bloqueios" {...a11yProps(0)} />
        <Tab label="Política" {...a11yProps(1)} />
        <Tab label="Solicitações" {...a11yProps(2)} />
      </Tabs>

      <AgendaTabPanel value={tab} index={0}>
      <Card sx={{ border: "1px solid", borderColor: "divider", mb: 2 }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>
            Bloqueios pontuais
          </Typography>

          <Stack spacing={1} sx={{ mb: 2 }}>
            {pontuais.map((b) => (
              <Stack key={b.id} direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <Chip label={TIPO_BLOQUEIO_LABEL.Pontual} size="small" />
                <Typography variant="body2">{formatarIntervaloPontual(b)}</Typography>
                <IconButton
                  size="small"
                  onClick={() => handleApagarBloqueio(b.id)}
                  aria-label={`Remover bloqueio pontual ${formatarIntervaloPontual(b)}`}
                >
                  <DeleteIcon fontSize="small" />
                </IconButton>
              </Stack>
            ))}
            {pontuais.length === 0 && (
              <Typography variant="body2" color="text.secondary">Nenhum bloqueio pontual cadastrado.</Typography>
            )}
          </Stack>

          <Grid container spacing={2} sx={{ alignItems: "flex-start" }}>
            <Grid size={{ xs: 12, sm: 4 }}>
              <DatePicker
                label="Data"
                format="DD/MM/YYYY"
                value={pontualData}
                onChange={(d) => setPontualData(d)}
                slotProps={DATE_FIELD_SLOTS}
              />
            </Grid>
            <Grid size={{ xs: 6, sm: 3 }}>
              <TextField
                label="Início"
                type="time"
                value={pontualHoraInicio}
                onChange={(e) => setPontualHoraInicio(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ inputLabel: { shrink: true } }}
              />
            </Grid>
            <Grid size={{ xs: 6, sm: 3 }}>
              <TextField
                label="Fim"
                type="time"
                value={pontualHoraFim}
                onChange={(e) => setPontualHoraFim(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ inputLabel: { shrink: true } }}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 2 }}>
              <Button
                startIcon={<AddIcon />}
                onClick={handleAdicionarPontual}
                disabled={!pontualData || !pontualHoraInicio || !pontualHoraFim || salvandoPontual}
                fullWidth
              >
                Adicionar
              </Button>
            </Grid>
            <Grid size={{ xs: 12 }}>
              <TextField
                label="Motivo (opcional)"
                value={pontualMotivo}
                onChange={(e) => setPontualMotivo(e.target.value)}
                size="small"
                fullWidth
              />
            </Grid>
          </Grid>
        </CardContent>
      </Card>

      <Card sx={{ border: "1px solid", borderColor: "divider", mb: 2 }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>
            Bloqueios recorrentes
          </Typography>

          <Stack spacing={1} sx={{ mb: 2 }}>
            {recorrentes.map((b) => (
              <Stack key={b.id} direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <Chip label={DIA_SEMANA_LABEL[b.diaSemana ?? -1] ?? String(b.diaSemana)} size="small" />
                <Typography variant="body2">{b.horaInicio?.slice(0, 5)} — {b.horaFim?.slice(0, 5)}</Typography>
                <IconButton
                  size="small"
                  onClick={() => handleApagarBloqueio(b.id)}
                  aria-label={`Remover bloqueio recorrente ${DIA_SEMANA_LABEL[b.diaSemana ?? -1] ?? ""} ${b.horaInicio?.slice(0, 5)}`}
                >
                  <DeleteIcon fontSize="small" />
                </IconButton>
              </Stack>
            ))}
            {recorrentes.length === 0 && (
              <Typography variant="body2" color="text.secondary">Nenhum bloqueio recorrente cadastrado.</Typography>
            )}
          </Stack>

          <Grid container spacing={2} sx={{ alignItems: "flex-start" }}>
            <Grid size={{ xs: 12, sm: 4 }}>
              <TextField
                select
                label="Dia da semana"
                value={recorrenteDiaSemana}
                onChange={(e) => setRecorrenteDiaSemana(Number(e.target.value))}
                size="small"
                fullWidth
              >
                {DIAS_SEMANA_OPTIONS.map((d) => (
                  <MenuItem key={d.value} value={d.value}>{d.label}</MenuItem>
                ))}
              </TextField>
            </Grid>
            <Grid size={{ xs: 6, sm: 3 }}>
              <TextField
                label="Início"
                type="time"
                value={recorrenteHoraInicio}
                onChange={(e) => setRecorrenteHoraInicio(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ inputLabel: { shrink: true } }}
              />
            </Grid>
            <Grid size={{ xs: 6, sm: 3 }}>
              <TextField
                label="Fim"
                type="time"
                value={recorrenteHoraFim}
                onChange={(e) => setRecorrenteHoraFim(e.target.value)}
                size="small"
                fullWidth
                slotProps={{ inputLabel: { shrink: true } }}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 2 }}>
              <Button
                startIcon={<AddIcon />}
                onClick={handleAdicionarRecorrente}
                disabled={!recorrenteHoraInicio || !recorrenteHoraFim || salvandoRecorrente}
                fullWidth
              >
                Adicionar
              </Button>
            </Grid>
            <Grid size={{ xs: 12 }}>
              <TextField
                label="Motivo (opcional)"
                value={recorrenteMotivo}
                onChange={(e) => setRecorrenteMotivo(e.target.value)}
                size="small"
                fullWidth
              />
            </Grid>
            {recorrenteError && (
              <Grid size={{ xs: 12 }}>
                <Typography variant="caption" color="error">{recorrenteError}</Typography>
              </Grid>
            )}
          </Grid>
        </CardContent>
      </Card>
      </AgendaTabPanel>

      <AgendaTabPanel value={tab} index={1}>
      <Card sx={{ border: "1px solid", borderColor: "divider" }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>
            Política de agenda
          </Typography>

          <Grid container spacing={2}>
            <Grid size={{ xs: 12, sm: 6 }}>
              <TextField
                label="Antecedência mínima (horas)"
                type="number"
                value={antecedenciaMinimaHoras}
                onChange={(e) => setAntecedenciaMinimaHoras(Number(e.target.value))}
                size="small"
                fullWidth
                slotProps={{ htmlInput: { min: 0 } }}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <TextField
                label="Horizonte (dias)"
                type="number"
                value={horizonteDias}
                onChange={(e) => setHorizonteDias(Number(e.target.value))}
                size="small"
                fullWidth
                slotProps={{ htmlInput: { min: 1, max: 365 } }}
              />
            </Grid>
          </Grid>

          <Divider sx={{ my: 2 }} />

          <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
            <Button variant="contained" onClick={handleSalvarPolitica} disabled={salvandoPolitica}>
              Salvar política
            </Button>
          </Box>
        </CardContent>
      </Card>
      </AgendaTabPanel>

      <AgendaTabPanel value={tab} index={2}>
        <SolicitacoesTab />
      </AgendaTabPanel>
    </Box>
  );
}
