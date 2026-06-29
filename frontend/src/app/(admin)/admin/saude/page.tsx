"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, Card, CardContent, Button, Stack, Switch,
  FormControlLabel, TextField, Chip, Divider,
} from "@mui/material";
import SendIcon from "@mui/icons-material/Send";
import AlertBanner from "@/components/ui/AlertBanner";
import PageHeader from "@/components/ui/PageHeader";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import { adminApi } from "@/lib/api/admin";
import type { HealthSnapshotResponse, StatusSaude } from "@/types";
import { extractApiError } from "@/lib/api/extractApiError";

const STATUS_COLOR: Record<StatusSaude, "success" | "warning" | "error"> = {
  Ok: "success",
  Degradado: "warning",
  Falha: "error",
};

function parseDestinatarios(texto: string): string[] {
  return texto
    .split(/[\n,;]+/)
    .map((x) => x.trim())
    .filter((x) => x.length > 0);
}

export default function SaudeAdminPage() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [ativo, setAtivo] = useState(false);
  const [hora, setHora] = useState("07:00");
  const [destinatarios, setDestinatarios] = useState("");
  const [incluirLiveness, setIncluirLiveness] = useState(true);
  const [incluirKpis, setIncluirKpis] = useState(true);
  const [incluirEntregabilidade, setIncluirEntregabilidade] = useState(true);
  const [incluirErros, setIncluirErros] = useState(true);
  const [ultimoEnvioEm, setUltimoEnvioEm] = useState<string | null>(null);

  const [ultimoSnapshot, setUltimoSnapshot] = useState<HealthSnapshotResponse | null>(null);
  const [snapshotIndisponivel, setSnapshotIndisponivel] = useState(false);

  const loadSnapshots = async () => {
    try {
      const res = await adminApi.listHealthSnapshots({ limite: 1 });
      setUltimoSnapshot(res.data[0] ?? null);
      setSnapshotIndisponivel(false);
    } catch {
      // snapshot é informativo; falha não bloqueia a página — placeholder distingue de "sem dados"
      setSnapshotIndisponivel(true);
    }
  };

  const load = async () => {
    setLoading(true);
    try {
      const [res] = await Promise.all([adminApi.getHealthReportConfig(), loadSnapshots()]);
      if (res.status !== 204 && res.data) {
        const c = res.data;
        setAtivo(c.ativo);
        setHora(c.horaEnvioUtc.slice(0, 5));
        setDestinatarios(c.destinatarios.join("\n"));
        setIncluirLiveness(c.incluirLiveness);
        setIncluirKpis(c.incluirKpis);
        setIncluirEntregabilidade(c.incluirEntregabilidade);
        setIncluirErros(c.incluirErros);
        setUltimoEnvioEm(c.ultimoEnvioEm);
      }
    } catch (err) {
      setError(extractApiError(err, "Erro ao carregar a configuração do relatório de saúde."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSalvar = async () => {
    setSaving(true);
    setError("");
    try {
      const res = await adminApi.updateHealthReportConfig({
        ativo,
        horaEnvioUtc: `${hora}:00`,
        destinatarios: parseDestinatarios(destinatarios),
        incluirLiveness,
        incluirKpis,
        incluirEntregabilidade,
        incluirErros,
      });
      setUltimoEnvioEm(res.data.ultimoEnvioEm);
      setSuccess("Configuração salva.");
    } catch (err) {
      setError(extractApiError(err, "Erro ao salvar. Verifique os destinatários quando o relatório está ativo."));
    } finally {
      setSaving(false);
    }
  };

  const handleEnviarAgora = async () => {
    setRunning(true);
    setError("");
    try {
      await adminApi.runHealthReport();
      setSuccess("Relatório enviado e snapshot gerado.");
      await loadSnapshots();
    } catch (err) {
      setError(extractApiError(err, "Erro ao executar o relatório. Salve uma configuração antes de enviar."));
    } finally {
      setRunning(false);
    }
  };

  if (loading) return <LoadingSpinner />;

  return (
    <Box>
      <PageHeader title="Relatório de saúde" />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent>
          <Stack spacing={2}>
            <FormControlLabel
              control={<Switch checked={ativo} onChange={(e) => setAtivo(e.target.checked)} />}
              label="Envio diário ativo"
            />
            <TextField
              label="Hora de envio (UTC)"
              type="time"
              value={hora}
              onChange={(e) => setHora(e.target.value)}
              size="small"
              sx={{ maxWidth: 200 }}
            />
            <TextField
              label="Destinatários"
              value={destinatarios}
              onChange={(e) => setDestinatarios(e.target.value)}
              size="small"
              fullWidth
              multiline
              rows={3}
              placeholder="Um e-mail por linha (ou separados por vírgula)"
              helperText="Obrigatório ao menos um quando o envio está ativo."
            />

            <Divider />
            <Typography variant="subtitle2" color="text.secondary">Seções do relatório</Typography>
            <FormControlLabel control={<Switch checked={incluirLiveness} onChange={(e) => setIncluirLiveness(e.target.checked)} />} label="Infraestrutura (liveness)" />
            <FormControlLabel control={<Switch checked={incluirKpis} onChange={(e) => setIncluirKpis(e.target.checked)} />} label="Indicadores (KPIs)" />
            <FormControlLabel control={<Switch checked={incluirEntregabilidade} onChange={(e) => setIncluirEntregabilidade(e.target.checked)} />} label="Entregabilidade de e-mail" />
            <FormControlLabel control={<Switch checked={incluirErros} onChange={(e) => setIncluirErros(e.target.checked)} />} label="Erros (24h)" />

            {ultimoEnvioEm && (
              <Typography variant="caption" color="text.secondary">
                Último envio: {new Date(ultimoEnvioEm).toLocaleString("pt-BR")}
              </Typography>
            )}

            <Stack direction="row" spacing={1}>
              <Button variant="contained" disabled={saving} onClick={handleSalvar}>
                Salvar
              </Button>
              <Button variant="outlined" startIcon={<SendIcon />} disabled={running} onClick={handleEnviarAgora}>
                Enviar agora
              </Button>
            </Stack>
          </Stack>
        </CardContent>
      </Card>

      <Card variant="outlined">
        <CardContent>
          <Typography variant="subtitle1" sx={{ mb: 1 }}>Último snapshot</Typography>
          {ultimoSnapshot ? (
            <Stack spacing={0.5}>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                <Chip label={ultimoSnapshot.statusGeral} size="small" color={STATUS_COLOR[ultimoSnapshot.statusGeral]} />
                <Typography variant="body2" color="text.secondary">{ultimoSnapshot.ambiente}</Typography>
              </Box>
              <Typography variant="caption" color="text.secondary">
                {new Date(ultimoSnapshot.capturadoEm).toLocaleString("pt-BR")}
              </Typography>
            </Stack>
          ) : snapshotIndisponivel ? (
            <Typography variant="body2" color="text.secondary">Snapshots indisponíveis no momento.</Typography>
          ) : (
            <Typography variant="body2" color="text.secondary">Nenhum snapshot ainda.</Typography>
          )}
        </CardContent>
      </Card>
    </Box>
  );
}
