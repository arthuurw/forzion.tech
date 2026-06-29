"use client";
import { Box, Typography, Button, CircularProgress } from "@mui/material";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useState } from "react";
import Link from "next/link";
import FormTextField from "@/components/forms/FormTextField";
import AlertBanner from "@/components/ui/AlertBanner";
import PageHeader from "@/components/ui/PageHeader";
import { emailSchema } from "@/lib/validations/common";

const schema = z.object({ email: emailSchema });
type FormData = z.infer<typeof schema>;

export default function ForgotPasswordPage() {
  const [loading, setLoading] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState("");

  const methods = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { email: "" },
  });

  const onSubmit = async (data: FormData) => {
    setError("");
    setLoading(true);
    try {
      await fetch("/api/auth/forgot-password", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: data.email }),
      });
      setSent(true);
    } catch {
      setError("Não foi possível conectar ao servidor.");
    } finally {
      setLoading(false);
    }
  };

  if (sent) {
    return (
      <Box>
        <PageHeader
          title="Verifique seu e-mail"
          subtitle="Se houver uma conta associada ao e-mail informado, você receberá um link de redefinição de senha em breve. O link é válido por 1 hora."
        />
        <Button component={Link} href="/login" variant="outlined" fullWidth>
          Voltar ao login
        </Button>
      </Box>
    );
  }

  return (
    <Box>
      <PageHeader
        title="Esqueceu a senha?"
        subtitle="Informe seu e-mail e enviaremos um link para redefinição de senha."
      />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <FormProvider {...methods}>
        <Box
          component="form"
          onSubmit={methods.handleSubmit(onSubmit)}
          sx={{ display: "flex", flexDirection: "column", gap: 2 }}
        >
          <FormTextField
            name="email"
            label="E-mail"
            type="email"
            required
            autoComplete="email"
          />
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
            Enviar link
          </Button>
        </Box>
      </FormProvider>

      <Box sx={{ mt: 3, textAlign: "center" }}>
        <Typography variant="body2" color="text.secondary">
          Lembrou a senha?{" "}
          <Link href="/login" style={{ color: "#1A1A1A", fontWeight: 600 }}>
            Fazer login
          </Link>
        </Typography>
      </Box>
    </Box>
  );
}
