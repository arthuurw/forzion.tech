"use client";
import { useEffect, useState } from "react";
import {
  Dialog, DialogTitle, DialogContent, DialogActions, Button, TextField, Stack, Typography,
} from "@mui/material";
import AlertBanner from "@/components/ui/AlertBanner";
import { mfaApi, MfaFator, type MfaFatorValue } from "@/lib/api/mfa";
import { extractApiError } from "@/lib/api/extractApiError";

interface StepUpDialogProps {
  open: boolean;
  title?: string;
  description?: string;
  onClose: () => void;
  onVerified: (token: string) => void;
}

export default function StepUpDialog({ open, title, description, onClose, onVerified }: StepUpDialogProps) {
  const [fator, setFator] = useState<MfaFatorValue | null>(null);
  const [codigo, setCodigo] = useState("");
  const [iniciando, setIniciando] = useState(false);
  const [verificando, setVerificando] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!open) return;
    setFator(null);
    setCodigo("");
    setError("");
    setIniciando(true);
    mfaApi
      .iniciarStepUp()
      .then((res) => setFator(res.data.fator))
      .catch((err) => setError(extractApiError(err, "Não foi possível iniciar a verificação.")))
      .finally(() => setIniciando(false));
  }, [open]);

  const handleVerificar = async () => {
    if (!codigo.trim()) return;
    setVerificando(true);
    setError("");
    try {
      const res = await mfaApi.verificarStepUp(codigo.trim());
      onVerified(res.data.token);
    } catch (err) {
      setError(extractApiError(err, "Código inválido. Tente novamente."));
    } finally {
      setVerificando(false);
    }
  };

  const instrucao = fator === MfaFator.Totp
    ? "Digite o código atual do seu aplicativo autenticador."
    : "Enviamos um código para o seu e-mail. Digite-o abaixo.";

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{title ?? "Confirmar identidade"}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {description && <Typography variant="body2" color="text.secondary">{description}</Typography>}
          <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
          {!iniciando && fator !== null && (
            <>
              <Typography variant="body2">{instrucao}</Typography>
              <TextField
                label="Código de verificação"
                value={codigo}
                onChange={(e) => setCodigo(e.target.value)}
                slotProps={{ htmlInput: { inputMode: "numeric", autoComplete: "one-time-code", "aria-label": "Código de verificação" } }}
                autoFocus
              />
            </>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button
          variant="contained"
          disabled={iniciando || verificando || !codigo.trim() || fator === null}
          onClick={handleVerificar}
        >
          {verificando ? "Verificando..." : "Confirmar"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
