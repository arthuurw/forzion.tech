"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, Card, CardContent, Button, Stack, Switch,
  FormControlLabel, Chip, Divider,
} from "@mui/material";
import SendIcon from "@mui/icons-material/Send";
import { useForm, FormProvider, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import FormTextField from "@/components/forms/FormTextField";
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

const saudeSchema = z
  .object({
    ativo: z.boolean(),
    hora: z.string().regex(/^\d{2}:\d{2}$/, "Horário inválido."),
    destinatarios: z.string(),
    incluirLiveness: z.boolean(),
    incluirKpis: z.boolean(),
    incluirEntregabilidade: z.boolean(),
    incluirErros: z.boolean(),
  })
  .superRefine((data, ctx) => {
    if (!data.ativo) return;
    const emails = parseDestinatarios(data.destinatarios);
    if (emails.length === 0) {
      ctx.addIssue({
        code: "custom",
        message: "Informe ao menos um destinatário quando o envio está ativo.",
        path: ["destinatarios"],
      });
      return;
    }
    for (const email of emails) {
      if (!z.string().email().safeParse(email).success) {
        ctx.addIssue({
          code: "custom",
          message: "Informe e-mails válidos.",
          path: ["destinatarios"],
        });
        return;
      }
    }
  });
type SaudeForm = z.infer<typeof saudeSchema>;

export default function SaudeAdminPage() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [ultimoEnvioEm, setUltimoEnvioEm] = useState<string | null>(null);
  const [ultimoSnapshot, setUltimoSnapshot] = useState<HealthSnapshotResponse | null>(null);
  const [snapshotIndisponivel, setSnapshotIndisponivel] = useState(false);

  const form = useForm<SaudeForm>({
    resolver: zodResolver(saudeSchema),
    defaultValues: {
      ativo: false,
      hora: "07:00",
      destinatarios: "",
      incluirLiveness: true,
      incluirKpis: true,
      incluirEntregabilidade: true,
      incluirErros: true,
    },
  });

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      try {
        const [res] = await Promise.all([
          adminApi.getHealthReportConfig(),
          (async () => {
            try {
              const snap = await adminApi.listHealthSnapshots({ limite: 1 });
              setUltimoSnapshot(snap.data[0] ?? null);
              setSnapshotIndisponivel(false);
            } catch {
              setSnapshotIndisponivel(true);
            }
          })(),
        ]);
        if (res.status !== 204 && res.data) {
          const c = res.data;
          form.reset({
            ativo: c.ativo,
            hora: c.horaEnvioUtc.slice(0, 5),
            destinatarios: c.destinatarios.join("\n"),
            incluirLiveness: c.incluirLiveness,
            incluirKpis: c.incluirKpis,
            incluirEntregabilidade: c.incluirEntregabilidade,
            incluirErros: c.incluirErros,
          });
          setUltimoEnvioEm(c.ultimoEnvioEm);
        }
      } catch (err) {
        setError(extractApiError(err, "Erro ao carregar a configuração do relatório de saúde."));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  const handleSalvar = form.handleSubmit(async (data) => {
    setSaving(true);
    setError("");
    try {
      const res = await adminApi.updateHealthReportConfig({
        ativo: data.ativo,
        horaEnvioUtc: `${data.hora}:00`,
        destinatarios: parseDestinatarios(data.destinatarios),
        incluirLiveness: data.incluirLiveness,
        incluirKpis: data.incluirKpis,
        incluirEntregabilidade: data.incluirEntregabilidade,
        incluirErros: data.incluirErros,
      });
      setUltimoEnvioEm(res.data.ultimoEnvioEm);
      setSuccess("Configuração salva.");
    } catch (err) {
      setError(extractApiError(err, "Erro ao salvar. Verifique os destinatários quando o relatório está ativo."));
    } finally {
      setSaving(false);
    }
  });

  const handleEnviarAgora = async () => {
    setRunning(true);
    setError("");
    try {
      await adminApi.runHealthReport();
      setSuccess("Relatório enviado e snapshot gerado.");
      try {
        const snap = await adminApi.listHealthSnapshots({ limite: 1 });
        setUltimoSnapshot(snap.data[0] ?? null);
        setSnapshotIndisponivel(false);
      } catch {
        setSnapshotIndisponivel(true);
      }
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
          <FormProvider {...form}>
            <Stack component="form" onSubmit={handleSalvar} noValidate spacing={2}>
              <Controller
                name="ativo"
                control={form.control}
                render={({ field }) => (
                  <FormControlLabel
                    control={
                      <Switch checked={field.value} onChange={(e) => field.onChange(e.target.checked)} />
                    }
                    label="Envio diário ativo"
                  />
                )}
              />
              <FormTextField
                name="hora"
                label="Hora de envio (UTC)"
                type="time"
                size="small"
                sx={{ maxWidth: 200 }}
              />
              <FormTextField
                name="destinatarios"
                label="Destinatários"
                size="small"
                fullWidth
                multiline
                rows={3}
                placeholder="Um e-mail por linha (ou separados por vírgula)"
                helperText="Obrigatório ao menos um quando o envio está ativo."
              />
              <Divider />
              <Typography variant="subtitle2" color="text.secondary">Seções do relatório</Typography>
              <Controller
                name="incluirLiveness"
                control={form.control}
                render={({ field }) => (
                  <FormControlLabel
                    control={<Switch checked={field.value} onChange={(e) => field.onChange(e.target.checked)} />}
                    label="Infraestrutura (liveness)"
                  />
                )}
              />
              <Controller
                name="incluirKpis"
                control={form.control}
                render={({ field }) => (
                  <FormControlLabel
                    control={<Switch checked={field.value} onChange={(e) => field.onChange(e.target.checked)} />}
                    label="Indicadores (KPIs)"
                  />
                )}
              />
              <Controller
                name="incluirEntregabilidade"
                control={form.control}
                render={({ field }) => (
                  <FormControlLabel
                    control={<Switch checked={field.value} onChange={(e) => field.onChange(e.target.checked)} />}
                    label="Entregabilidade de e-mail"
                  />
                )}
              />
              <Controller
                name="incluirErros"
                control={form.control}
                render={({ field }) => (
                  <FormControlLabel
                    control={<Switch checked={field.value} onChange={(e) => field.onChange(e.target.checked)} />}
                    label="Erros (24h)"
                  />
                )}
              />
              {ultimoEnvioEm && (
                <Typography variant="caption" color="text.secondary">
                  Último envio: {new Date(ultimoEnvioEm).toLocaleString("pt-BR")}
                </Typography>
              )}
              <Stack direction="row" spacing={1}>
                <Button type="submit" variant="contained" disabled={saving}>
                  Salvar
                </Button>
                <Button variant="outlined" startIcon={<SendIcon />} disabled={running} onClick={handleEnviarAgora}>
                  Enviar agora
                </Button>
              </Stack>
            </Stack>
          </FormProvider>
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
