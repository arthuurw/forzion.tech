"use client";
import { Box, Typography, Button, CircularProgress } from "@mui/material";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useState } from "react";
import FormTextField from "@/components/forms/FormTextField";
import PasswordField from "@/components/forms/PasswordField";
import AlertBanner from "@/components/ui/AlertBanner";
import { useAuth } from "@/lib/auth/context";
import { loginSchema, type LoginFormData } from "@/lib/validations/common";
import type { LoginResponse, ProblemDetails } from "@/types";

export default function LoginPage() {
  const { login } = useAuth();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const methods = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "" },
  });

  const onSubmit = async (data: LoginFormData) => {
    setError("");
    setLoading(true);
    try {
      const res = await fetch("/api/auth", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });

      if (!res.ok) {
        const problem: ProblemDetails = await res.json();
        setError(problem.detail ?? problem.title ?? "Erro ao fazer login.");
        return;
      }

      const payload: LoginResponse = await res.json();
      login(payload);
    } catch {
      setError("Não foi possível conectar ao servidor.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 0.5 }}>
        Entrar
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Acesse sua conta para continuar.
      </Typography>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <FormProvider {...methods}>
        <Box
          component="form"
          onSubmit={methods.handleSubmit(onSubmit)}
          sx={{ display: "flex", flexDirection: "column", gap: 2 }}
        >
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
          >
            Entrar
          </Button>
        </Box>
      </FormProvider>

      <Box sx={{ mt: 3, textAlign: "center" }}>
        <Typography variant="body2" color="text.secondary">
          Ainda não tem conta?{" "}
          <Link href="/cadastro/treinador" style={{ color: "inherit", fontWeight: 600 }}>
            Cadastre-se como treinador
          </Link>
        </Typography>
      </Box>
    </Box>
  );
}
