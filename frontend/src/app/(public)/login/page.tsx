"use client";
import { Box, Typography, Button, CircularProgress, Divider } from "@mui/material";
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
import type { LoginResponse, ProblemDetails } from "@/types";
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
      const res = await fetch("/api/auth", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: data.email, senha: data.password }),
      });

      if (!res.ok) {
        if (res.status === 401) {
          setError("Credenciais inválidas.");
        } else if (res.status >= 500) {
          setError("Erro interno. Tente novamente.");
        } else {
          const problem: ProblemDetails = await res.json();
          if (res.status === 403 && problem.code === "EMAIL_NAO_VERIFICADO") {
            setEmailNaoVerificado(data.email);
          } else if (res.status === 403 && problem.code === "TREINADOR_AGUARDANDO_APROVACAO") {
            setBloqueio({
              titulo: "Cadastro em análise",
              mensagem: "Seu cadastro de treinador está em análise. Aguarde a aprovação do administrador para acessar.",
            });
          } else if (res.status === 403 && problem.code === "TREINADOR_INATIVO") {
            setBloqueio({
              titulo: "Conta inativa",
              mensagem: "Sua conta de treinador está inativa. Entre em contato com o suporte.",
            });
          } else {
            setError(problem.title ?? "Erro ao fazer login.");
          }
        }
        return;
      }

      const payload: LoginResponse = await res.json();
      login(payload);
      router.push(homeRouteFor(payload.tipoConta));
    } catch {
      setError("Não foi possível conectar ao servidor.");
    } finally {
      setLoading(false);
    }
  };

  const handleReenviar = async () => {
    if (!emailNaoVerificado) return;
    setReenviando(true);
    try {
      await fetch("/api/auth/resend-verification", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: emailNaoVerificado }),
      });
      setReenviado(true);
    } catch {
      setError("Não foi possível reenviar o e-mail de verificação.");
    } finally {
      setReenviando(false);
    }
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
    </Box>
  );
}
