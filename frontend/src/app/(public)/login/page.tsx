"use client";
import {
  Box, Typography, Button, CircularProgress, Divider, TextField, Checkbox, FormControlLabel, Stack,
} from "@mui/material";
import { alpha } from "@mui/material/styles";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import FormTextField from "@/components/forms/FormTextField";
import PasswordField from "@/components/forms/PasswordField";
import AlertBanner from "@/components/ui/AlertBanner";
import { useAuth, homeRouteFor } from "@/lib/auth/context";
import { loginSchema, type LoginFormData } from "@/lib/validations/common";
import { authApi, AuthApiError } from "@/lib/api/auth";
import { MfaFator, type MfaFatorValue } from "@/lib/api/mfa";
import LoadingSpinner from "@/components/ui/LoadingSpinner";

export default function LoginPage() {
  const { login, user, isLoading } = useAuth();
  const router = useRouter();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [emailNaoVerificado, setEmailNaoVerificado] = useState<string | null>(null);
  const [reenviando, setReenviando] = useState(false);
  const [reenviado, setReenviado] = useState(false);
  const [bloqueio, setBloqueio] = useState<{ titulo: string; mensagem: string } | null>(null);
  const [mfaPendente, setMfaPendente] = useState(false);
  const [mfaCodigo, setMfaCodigo] = useState("");
  const [mfaFator, setMfaFator] = useState<MfaFatorValue>(MfaFator.Totp);
  const [lembrar, setLembrar] = useState(false);
  const [emailEnviado, setEmailEnviado] = useState(false);
  const [mfaError, setMfaError] = useState("");
  const methods = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "" },
  });

  useEffect(() => {
    if (!isLoading && user) {
      router.replace(homeRouteFor(user.tipoConta));
    }
  }, [isLoading, user, router]);

  if (isLoading || user) return <LoadingSpinner fullPage />;

  const onSubmit = async (data: LoginFormData) => {
    setError("");
    setEmailNaoVerificado(null);
    setReenviado(false);
    setBloqueio(null);
    setLoading(true);
    try {
      const payload = await authApi.login({ email: data.email, senha: data.password });
      if ("contaId" in payload) {
        login(payload);
        router.push(homeRouteFor(payload.tipoConta));
      } else {
        setMfaPendente(true);
      }
    } catch (e) {
      if (!(e instanceof AuthApiError)) {
        setError("Não foi possível conectar ao servidor.");
        return;
      }
      if (e.status === 401) {
        setError("Credenciais inválidas.");
      } else if (e.status >= 500) {
        setError("Erro interno. Tente novamente.");
      } else if (e.status === 403 && e.problem?.code === "EMAIL_NAO_VERIFICADO") {
        setEmailNaoVerificado(data.email);
      } else if (e.status === 403 && e.problem?.code === "TREINADOR_AGUARDANDO_APROVACAO") {
        setBloqueio({
          titulo: "Cadastro em análise",
          mensagem: "Seu cadastro de treinador está em análise. Aguarde a aprovação do administrador para acessar.",
        });
      } else if (e.status === 403 && e.problem?.code === "TREINADOR_INATIVO") {
        setBloqueio({
          titulo: "Conta inativa",
          mensagem: "Sua conta de treinador está inativa. Entre em contato com o suporte.",
        });
      } else {
        setError(e.problem?.title ?? "Erro ao fazer login.");
      }
    } finally {
      setLoading(false);
    }
  };

  const handleReenviar = async () => {
    if (!emailNaoVerificado) return;
    setReenviando(true);
    try {
      await authApi.resendVerification(emailNaoVerificado);
      setReenviado(true);
    } catch {
      setError("Não foi possível reenviar o e-mail de verificação.");
    } finally {
      setReenviando(false);
    }
  };

  const completarMfa = async () => {
    if (!mfaCodigo.trim()) return;
    setMfaError("");
    setLoading(true);
    try {
      const payload = await authApi.completarMfa({
        codigo: mfaCodigo.trim(),
        fator: mfaFator,
        lembrarDispositivo: lembrar,
      });
      login(payload);
      router.push(homeRouteFor(payload.tipoConta));
    } catch (e) {
      if (e instanceof AuthApiError) {
        setMfaError(e.problem?.detail ?? e.problem?.title ?? "Código inválido.");
      } else {
        setMfaError("Não foi possível conectar ao servidor.");
      }
    } finally {
      setLoading(false);
    }
  };

  const usarCodigoEmail = async () => {
    setMfaError("");
    try {
      await authApi.enviarCodigoEmailMfa();
      setMfaFator(MfaFator.Email);
      setMfaCodigo("");
      setEmailEnviado(true);
    } catch {
      setMfaError("Não foi possível enviar o código por e-mail.");
    }
  };

  const usarRecovery = () => {
    setMfaError("");
    setMfaFator(MfaFator.RecoveryCode);
    setMfaCodigo("");
    setEmailEnviado(false);
  };

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 0.5 }}>
        Acesse sua conta
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Informe suas credenciais para acessar a plataforma.
      </Typography>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      {mfaPendente ? (
        <Box>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {mfaFator === MfaFator.Email
              ? "Digite o código que enviamos para o seu e-mail."
              : mfaFator === MfaFator.RecoveryCode
                ? "Digite um dos seus códigos de recuperação."
                : "Digite o código atual do seu aplicativo autenticador."}
          </Typography>
          <AlertBanner open={!!mfaError} message={mfaError} onClose={() => setMfaError("")} />
          {emailEnviado && mfaFator === MfaFator.Email && (
            <AlertBanner
              open
              severity="success"
              message="Código enviado para o seu e-mail."
              onClose={() => setEmailEnviado(false)}
            />
          )}
          <Box
            component="form"
            onSubmit={(e) => { e.preventDefault(); completarMfa(); }}
            sx={{ display: "flex", flexDirection: "column", gap: 2 }}
          >
            <TextField
              label={mfaFator === MfaFator.RecoveryCode ? "Código de recuperação" : "Código de verificação"}
              value={mfaCodigo}
              onChange={(e) => setMfaCodigo(e.target.value)}
              slotProps={{ htmlInput: { inputMode: mfaFator === MfaFator.RecoveryCode ? "text" : "numeric", autoComplete: "one-time-code", "aria-label": "Código de verificação" } }}
              autoFocus
            />
            <FormControlLabel
              control={<Checkbox checked={lembrar} onChange={(e) => setLembrar(e.target.checked)} />}
              label="Lembrar este dispositivo por 30 dias"
            />
            <Button
              type="submit"
              variant="contained"
              color="primary"
              size="large"
              fullWidth
              disabled={loading || !mfaCodigo.trim()}
              startIcon={loading ? <CircularProgress size={18} color="inherit" /> : undefined}
            >
              Verificar
            </Button>
          </Box>
          <Stack spacing={1} sx={{ mt: 2 }}>
            {mfaFator !== MfaFator.Email && (
              <Button variant="text" size="small" onClick={usarCodigoEmail}>
                Usar código por e-mail
              </Button>
            )}
            {mfaFator !== MfaFator.RecoveryCode && (
              <Button variant="text" size="small" onClick={usarRecovery}>
                Usar código de recuperação
              </Button>
            )}
          </Stack>
        </Box>
      ) : (
        <>
      {emailNaoVerificado && (
        <Box
          sx={{
            mb: 2,
            p: 2,
            borderRadius: 1,
            bgcolor: (theme) => alpha(theme.palette.primary.main, 0.12),
            border: "1px solid",
            borderColor: "primary.main",
          }}
        >
          <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5 }}>
            E-mail ainda não verificado
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
            {reenviado
              ? "E-mail de verificação reenviado. Confira sua caixa de entrada."
              : "Confirme seu e-mail antes de entrar. Não recebeu o link?"}
          </Typography>
          {!reenviado && (
            <Button
              variant="contained"
              color="primary"
              size="small"
              onClick={handleReenviar}
              disabled={reenviando}
              startIcon={reenviando ? <CircularProgress size={16} color="inherit" /> : undefined}
            >
              Reenviar verificação
            </Button>
          )}
        </Box>
      )}

      {bloqueio && (
        <Box
          sx={{
            mb: 2,
            p: 2,
            borderRadius: 1,
            bgcolor: (theme) => alpha(theme.palette.primary.main, 0.12),
            border: "1px solid",
            borderColor: "primary.main",
          }}
        >
          <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5 }}>
            {bloqueio.titulo}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {bloqueio.mensagem}
          </Typography>
        </Box>
      )}

      <FormProvider {...methods}>
        <Box component="form" onSubmit={methods.handleSubmit(onSubmit)} sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          <FormTextField name="email" label="E-mail" type="email" required autoComplete="email" />
          <PasswordField name="password" label="Senha" required autoComplete="current-password" />

          <Button
            type="submit"
            variant="contained"
            color="primary"
            size="large"
            fullWidth
            disabled={loading}
            startIcon={loading ? <CircularProgress size={18} color="inherit" /> : undefined}
            sx={{ mt: 0.5 }}
          >
            Entrar
          </Button>
        </Box>
      </FormProvider>

      <Divider sx={{ my: 3 }} />

      <Box sx={{ textAlign: "center" }}>
        <Typography variant="body2" color="text.secondary">
          Ainda não tem conta?{" "}
          <Link href="/cadastro/treinador" style={{ color: "#1A1A1A", fontWeight: 600 }}>
            Cadastre-se como treinador
          </Link>
          {" "}ou{" "}
          <Link href="/cadastro/aluno" style={{ color: "#1A1A1A", fontWeight: 600 }}>
            como aluno
          </Link>
        </Typography>
      </Box>
        </>
      )}
    </Box>
  );
}
