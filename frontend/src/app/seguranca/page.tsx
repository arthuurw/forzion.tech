"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, Card, CardContent, Stack, Button, Chip, Divider, TextField,
  Dialog, DialogTitle, DialogContent, DialogActions, List, ListItem, ListItemText,
} from "@mui/material";
import ShieldIcon from "@mui/icons-material/Shield";
import DevicesIcon from "@mui/icons-material/Devices";
import { QRCodeSVG } from "qrcode.react";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import PageHeader from "@/components/ui/PageHeader";
import StepUpDialog from "@/components/seguranca/StepUpDialog";
import RecoveryCodesPanel from "@/components/seguranca/RecoveryCodesPanel";
import { mfaApi, type MfaStatus, type IniciarTotpResult } from "@/lib/api/mfa";
import { extractApiError } from "@/lib/api/extractApiError";

type StepUpAction = "desabilitar" | "regenerar";

export default function SegurancaPage() {
  const [status, setStatus] = useState<MfaStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [enroll, setEnroll] = useState<IniciarTotpResult | null>(null);
  const [enrollCodigo, setEnrollCodigo] = useState("");
  const [enrollError, setEnrollError] = useState("");
  const [enrolling, setEnrolling] = useState(false);
  const [iniciandoEnroll, setIniciandoEnroll] = useState(false);

  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);
  const [stepUpAction, setStepUpAction] = useState<StepUpAction | null>(null);

  const carregarStatus = async () => {
    try {
      const res = await mfaApi.getStatus();
      setStatus(res.data);
    } catch (err) {
      setError(extractApiError(err, "Erro ao carregar configurações de segurança."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    carregarStatus();
  }, []);

  const iniciarAtivacao = async () => {
    setIniciandoEnroll(true);
    setError("");
    try {
      const res = await mfaApi.iniciarTotp();
      setEnroll(res.data);
      setEnrollCodigo("");
      setEnrollError("");
    } catch (err) {
      setError(extractApiError(err, "Não foi possível iniciar a ativação do 2FA."));
    } finally {
      setIniciandoEnroll(false);
    }
  };

  const confirmarAtivacao = async () => {
    if (!enrollCodigo.trim()) return;
    setEnrolling(true);
    setEnrollError("");
    try {
      const res = await mfaApi.confirmarTotp(enrollCodigo.trim());
      setEnroll(null);
      setRecoveryCodes(res.data.recoveryCodes);
      setSuccess("Autenticação de dois fatores ativada.");
      await carregarStatus();
    } catch (err) {
      setEnrollError(extractApiError(err, "Código inválido. Verifique o aplicativo e tente novamente."));
    } finally {
      setEnrolling(false);
    }
  };

  const onStepUpVerified = async (token: string) => {
    const action = stepUpAction;
    setStepUpAction(null);
    setError("");
    try {
      if (action === "desabilitar") {
        await mfaApi.desabilitar(token);
        setSuccess("Autenticação de dois fatores desativada.");
      } else if (action === "regenerar") {
        const res = await mfaApi.regenerarRecovery(token);
        setRecoveryCodes(res.data.recoveryCodes);
        setSuccess("Novos códigos de recuperação gerados.");
      }
      await carregarStatus();
    } catch (err) {
      setError(extractApiError(err, "Não foi possível concluir a operação."));
    }
  };

  if (loading) return <LoadingSpinner />;

  const habilitado = status?.habilitado ?? false;

  return (
    <Box sx={{ maxWidth: { xs: "100%", md: 580 } }}>
      <PageHeader
        title="Segurança"
        subtitle="Autenticação de dois fatores e dispositivos confiáveis"
      />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Card sx={{ mb: 2.5, border: "1px solid", borderColor: "divider" }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 2 }}>
            <Box sx={{ width: 36, height: 36, borderRadius: 2, bgcolor: "action.subtleBg", display: "flex", alignItems: "center", justifyContent: "center" }}>
              <ShieldIcon fontSize="small" sx={{ color: "text.secondary" }} />
            </Box>
            <Typography variant="subtitle1" component="h2">Autenticação de dois fatores</Typography>
            <Chip
              label={habilitado ? "Ativo" : "Inativo"}
              size="small"
              color={habilitado ? "success" : "default"}
              sx={{ ml: "auto" }}
            />
          </Box>

          {habilitado ? (
            <Stack spacing={2}>
              <Typography variant="body2" color="text.secondary">
                Sua conta está protegida por TOTP. Você tem{" "}
                <strong>{status?.recoveryCodesRestantes ?? 0}</strong> código(s) de recuperação restante(s).
              </Typography>
              <Divider />
              <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }}>
                <Button variant="contained" onClick={() => setStepUpAction("regenerar")}>
                  Regenerar códigos de recuperação
                </Button>
                <Button variant="contained" color="error" onClick={() => setStepUpAction("desabilitar")}>
                  Desativar 2FA
                </Button>
              </Stack>
            </Stack>
          ) : (
            <Stack spacing={2}>
              <Typography variant="body2" color="text.secondary">
                Adicione uma camada extra de proteção exigindo um código do seu aplicativo autenticador
                ao entrar e em ações sensíveis.
              </Typography>
              <Button
                variant="contained"
                onClick={iniciarAtivacao}
                disabled={iniciandoEnroll}
                sx={{ alignSelf: "flex-start" }}
              >
                {iniciandoEnroll ? "Preparando..." : "Ativar 2FA"}
              </Button>
            </Stack>
          )}
        </CardContent>
      </Card>

      {habilitado && (
        <Card sx={{ border: "1px solid", borderColor: "divider" }}>
          <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 2 }}>
              <Box sx={{ width: 36, height: 36, borderRadius: 2, bgcolor: "action.subtleBg", display: "flex", alignItems: "center", justifyContent: "center" }}>
                <DevicesIcon fontSize="small" sx={{ color: "text.secondary" }} />
              </Box>
              <Typography variant="subtitle1" component="h2">Dispositivos confiáveis</Typography>
            </Box>
            {status && status.dispositivos.length > 0 ? (
              <List disablePadding>
                {status.dispositivos.map((d) => (
                  <ListItem key={d.id} disableGutters>
                    <ListItemText
                      primary={d.rotulo ?? "Dispositivo"}
                      secondary={`Confiado em ${new Date(d.criadoEm).toLocaleDateString("pt-BR")} · expira em ${new Date(d.expiraEm).toLocaleDateString("pt-BR")}`}
                    />
                  </ListItem>
                ))}
              </List>
            ) : (
              <Typography variant="body2" color="text.secondary">
                Nenhum dispositivo confiável. Marque &quot;lembrar este dispositivo&quot; ao entrar para pular o
                segundo fator por 30 dias.
              </Typography>
            )}
          </CardContent>
        </Card>
      )}

      <Dialog open={!!enroll} onClose={() => setEnroll(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Ativar autenticação de dois fatores</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <Typography variant="body2" color="text.secondary">
              Escaneie o QR code com seu aplicativo autenticador (Google Authenticator, Authy, 1Password)
              ou insira a chave manualmente. Depois, digite o código gerado.
            </Typography>
            {enroll && (
              <Box sx={{ display: "flex", justifyContent: "center", p: 2, bgcolor: "background.paper", borderRadius: 2 }}>
                <QRCodeSVG value={enroll.otpauthUri} size={180} aria-label="QR code para configurar o autenticador" />
              </Box>
            )}
            <TextField
              label="Chave manual"
              value={enroll?.secretBase32 ?? ""}
              slotProps={{ input: { readOnly: true, sx: { fontFamily: "monospace" } } }}
            />
            <AlertBanner open={!!enrollError} message={enrollError} onClose={() => setEnrollError("")} />
            <TextField
              label="Código do aplicativo"
              value={enrollCodigo}
              onChange={(e) => setEnrollCodigo(e.target.value)}
              slotProps={{ htmlInput: { inputMode: "numeric", autoComplete: "one-time-code", "aria-label": "Código do aplicativo" } }}
              autoFocus
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEnroll(null)}>Cancelar</Button>
          <Button variant="contained" onClick={confirmarAtivacao} disabled={enrolling || !enrollCodigo.trim()}>
            {enrolling ? "Ativando..." : "Confirmar"}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={!!recoveryCodes} onClose={() => setRecoveryCodes(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Códigos de recuperação</DialogTitle>
        <DialogContent>
          {recoveryCodes && (
            <Box sx={{ pt: 1 }}>
              <RecoveryCodesPanel codigos={recoveryCodes} onConcluir={() => setRecoveryCodes(null)} />
            </Box>
          )}
        </DialogContent>
      </Dialog>

      <StepUpDialog
        open={stepUpAction !== null}
        title={stepUpAction === "desabilitar" ? "Desativar 2FA" : "Regenerar códigos"}
        description="Esta é uma ação sensível. Confirme sua identidade para continuar."
        onClose={() => setStepUpAction(null)}
        onVerified={onStepUpVerified}
      />
    </Box>
  );
}
