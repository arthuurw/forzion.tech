"use client";
import { useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Typography,
  Stack,
  FormControlLabel,
  Switch,
  Box,
  Divider,
} from "@mui/material";
import CookieIcon from "@mui/icons-material/Cookie";
import { useConsent } from "@/hooks/useConsent";

interface ConsentBannerProps {
  /**
   * When true, forces the dialog open even if the user already chose
   * (used from /perfil to reopen preferences).
   */
  forceOpen?: boolean;
  onClose?: () => void;
}

export default function ConsentBanner({ forceOpen, onClose }: ConsentBannerProps) {
  const { consent, acceptAll, acceptEssential, savePreferences } = useConsent();
  const [showPrefs, setShowPrefs] = useState(false);
  const [analyticsChecked, setAnalyticsChecked] = useState(
    consent?.analytics ?? false,
  );

  // Show when no choice made yet, or when forced open from /perfil
  const open = forceOpen || consent === null;

  if (!open) return null;

  const handleAcceptAll = () => {
    acceptAll();
    onClose?.();
  };

  const handleEssentialOnly = () => {
    acceptEssential();
    onClose?.();
  };

  const handleSavePrefs = () => {
    savePreferences(analyticsChecked);
    onClose?.();
  };

  return (
    <Dialog
      open
      aria-label="Consentimento de cookies e privacidade LGPD"
      maxWidth="xs"
      fullWidth
      onClose={forceOpen ? onClose : undefined}
      slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}
    >
      <DialogTitle>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <CookieIcon fontSize="small" color="action" />
          <span>Cookies e privacidade</span>
        </Box>
      </DialogTitle>
      <DialogContent>
        {!showPrefs ? (
          <Stack spacing={1.5}>
            <Typography variant="body2">
              Usamos cookies essenciais para manter sua sessão segura.
              Opcionalmente, cookies de análise nos ajudam a melhorar a
              plataforma. Sua escolha fica salva por 1 ano.
            </Typography>
            <Typography variant="caption" color="text.secondary">
              Você pode alterar suas preferências a qualquer momento em{" "}
              <strong>Perfil → Privacidade (LGPD)</strong>.
            </Typography>
          </Stack>
        ) : (
          <Stack spacing={2}>
            <Box>
              <FormControlLabel
                control={<Switch checked disabled />}
                label={
                  <Box>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      Essenciais
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      Sessão de autenticação (httpOnly). Sempre ativos.
                    </Typography>
                  </Box>
                }
              />
            </Box>
            <Divider />
            <Box>
              <FormControlLabel
                control={
                  <Switch
                    checked={analyticsChecked}
                    onChange={(e) => setAnalyticsChecked(e.target.checked)}
                  />
                }
                label={
                  <Box>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      Análise (Sentry)
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      Monitoramento de erros e performance. Desativado por
                      padrão.
                    </Typography>
                  </Box>
                }
              />
            </Box>
          </Stack>
        )}
      </DialogContent>
      <DialogActions
        sx={{
          flexWrap: "wrap",
          gap: 0.5,
          px: 2,
          pb: 2,
          // xs: empilha em coluna (3 botões espremidos a 360px); semântica inalterada.
          flexDirection: { xs: "column", sm: "row" },
          alignItems: { xs: "stretch", sm: "center" },
        }}
      >
        {!showPrefs ? (
          <>
            <Button
              size="small"
              onClick={() => setShowPrefs(true)}
              sx={{ mr: "auto" }}
            >
              Preferências
            </Button>
            <Button size="small" variant="outlined" onClick={handleEssentialOnly}>
              Só essenciais
            </Button>
            <Button size="small" variant="contained" onClick={handleAcceptAll}>
              Aceitar todos
            </Button>
          </>
        ) : (
          <>
            <Button
              size="small"
              onClick={() => setShowPrefs(false)}
              sx={{ mr: "auto" }}
            >
              Voltar
            </Button>
            <Button size="small" variant="contained" onClick={handleSavePrefs}>
              Salvar preferências
            </Button>
          </>
        )}
      </DialogActions>
    </Dialog>
  );
}
