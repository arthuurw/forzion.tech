"use client";
import { useEffect, useState } from "react";
import {
  Box, Card, CardContent, Button, Grid, TextField, Stack, Typography, IconButton, Chip,
} from "@mui/material";
import DeleteIcon from "@mui/icons-material/Delete";
import AddIcon from "@mui/icons-material/Add";
import { DatePicker } from "@mui/x-date-pickers/DatePicker";
import type { Dayjs } from "dayjs";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import PageHeader from "@/components/ui/PageHeader";
import { extractApiError } from "@/lib/api/extractApiError";
import { agendaApi } from "@/lib/api/agenda";
import type { BloqueioAgendaResponse } from "@/types";
import { TIPO_BLOQUEIO_LABEL, DIA_SEMANA_LABEL } from "@/lib/constants/labels";

const DATE_FIELD_SLOTS = {
  textField: { size: "small" as const, fullWidth: true },
};

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
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [bloqueios, setBloqueios] = useState<BloqueioAgendaResponse[]>([]);

  const [pontualData, setPontualData] = useState<Dayjs | null>(null);
  const [pontualHoraInicio, setPontualHoraInicio] = useState("");
  const [pontualHoraFim, setPontualHoraFim] = useState("");
  const [pontualMotivo, setPontualMotivo] = useState("");
  const [salvandoPontual, setSalvandoPontual] = useState(false);

  useEffect(() => {
    agendaApi
      .listarBloqueios()
      .then((res) => setBloqueios(res.data))
      .catch(() => setError("Não foi possível carregar os bloqueios de agenda."))
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
        inicioUtc: `${dia}T${pontualHoraInicio}:00.000Z`,
        fimUtc: `${dia}T${pontualHoraFim}:00.000Z`,
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

  const handleApagarBloqueio = async (id: string) => {
    setError("");
    try {
      await agendaApi.apagarBloqueio(id);
      setBloqueios((prev) => prev.filter((b) => b.id !== id));
    } catch (err) {
      setError(extractApiError(err, "Não foi possível apagar o bloqueio."));
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

          <Stack spacing={1}>
            {recorrentes.map((b) => (
              <Stack key={b.id} direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <Chip label={DIA_SEMANA_LABEL[b.diaSemana ?? -1] ?? String(b.diaSemana)} size="small" />
                <Typography variant="body2">{b.horaInicio?.slice(0, 5)} — {b.horaFim?.slice(0, 5)}</Typography>
              </Stack>
            ))}
            {recorrentes.length === 0 && (
              <Typography variant="body2" color="text.secondary">Nenhum bloqueio recorrente cadastrado.</Typography>
            )}
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
