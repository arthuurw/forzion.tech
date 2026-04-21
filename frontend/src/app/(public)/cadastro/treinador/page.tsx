"use client";
import { useState } from "react";
import { Box, Typography, Button, CircularProgress, Paper, Stack } from "@mui/material";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircle";
import Link from "next/link";
import FormTextField from "@/components/forms/FormTextField";
import PasswordField from "@/components/forms/PasswordField";
import AlertBanner from "@/components/ui/AlertBanner";
import {
  cadastroTreinadorSchema,
  type CadastroTreinadorFormData,
} from "@/lib/validations/common";
import type { ProblemDetails } from "@/types";

export default function CadastroTreinadorPage() {
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);

  const methods = useForm<CadastroTreinadorFormData>({
    resolver: zodResolver(cadastroTreinadorSchema),
    defaultValues: { nome: "", email: "", password: "", confirmPassword: "" },
  });

  const onSubmit = async (data: CadastroTreinadorFormData) => {
    setError("");
    setLoading(true);
    try {
      const res = await fetch("/api/auth/register/treinador", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ nome: data.nome, email: data.email, senha: data.password }),
      });

      if (!res.ok) {
        const problem: ProblemDetails = await res.json();
        setError(problem.detail ?? problem.title ?? "Erro ao criar conta.");
        return;
      }

      setSuccess(true);
    } catch {
      setError("Não foi possível conectar ao servidor.");
    } finally {
      setLoading(false);
    }
  };

  if (success) {
    return (
      <Paper elevation={0} variant="outlined" sx={{ p: 4, textAlign: "center" }}>
        <CheckCircleOutlineIcon sx={{ fontSize: 56, color: "primary.main", mb: 2 }} />
        <Typography variant="h6" sx={{ fontWeight: 700, mb: 1 }}>
          Solicitação enviada
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
          Seu cadastro está em análise. Você receberá o acesso assim que for validado pela equipe.
        </Typography>
        <Link href="/login" style={{ textDecoration: "none" }}>
          <Button variant="contained" color="primary">
            Ir para o login
          </Button>
        </Link>
      </Paper>
    );
  }

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 0.5 }}>
        Criar conta profissional
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Após o envio, seu cadastro passará por validação antes da liberação do acesso.
      </Typography>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <FormProvider {...methods}>
        <Stack
          component="form"
          onSubmit={methods.handleSubmit(onSubmit)}
          spacing={2}
        >
          <FormTextField name="nome" label="Nome completo" required autoComplete="name" />
          <FormTextField name="email" label="E-mail" type="email" required autoComplete="email" />
          <PasswordField name="password" label="Senha" required autoComplete="new-password" />
          <PasswordField name="confirmPassword" label="Confirmar senha" required autoComplete="new-password" />

          <Button
            type="submit"
            variant="contained"
            color="primary"
            size="large"
            fullWidth
            disabled={loading}
            startIcon={loading ? <CircularProgress size={18} color="inherit" /> : undefined}
          >
            Criar conta
          </Button>
        </Stack>
      </FormProvider>

      <Box sx={{ mt: 3, textAlign: "center" }}>
        <Typography variant="body2" color="text.secondary">
          Já tem conta?{" "}
          <Link href="/login" style={{ color: "inherit", fontWeight: 600 }}>
            Entrar
          </Link>
        </Typography>
      </Box>
    </Box>
  );
}
