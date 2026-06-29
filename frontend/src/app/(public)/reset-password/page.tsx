"use client";
import { Box, Button, CircularProgress } from "@mui/material";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useState, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import PasswordField from "@/components/forms/PasswordField";
import AlertBanner from "@/components/ui/AlertBanner";
import PageHeader from "@/components/ui/PageHeader";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import { registerPasswordSchema } from "@/lib/validations/common";
import type { ProblemDetails } from "@/types";

const schema = z
  .object({
    novaSenha: registerPasswordSchema,
    confirmarSenha: z.string().min(1, "Confirmação obrigatória"),
  })
  .refine((d) => d.novaSenha === d.confirmarSenha, {
    message: "As senhas não coincidem",
    path: ["confirmarSenha"],
  });

type FormData = z.infer<typeof schema>;

function ResetPasswordInner() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";

  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState("");

  const methods = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { novaSenha: "", confirmarSenha: "" },
  });

  if (!token) {
    return (
      <Box>
        <PageHeader
          title="Link inválido"
          subtitle="O link de redefinição é inválido ou foi removido da URL."
        />
        <Button component={Link} href="/forgot-password" variant="outlined" fullWidth>
          Solicitar novo link
        </Button>
      </Box>
    );
  }

  const onSubmit = async (data: FormData) => {
    setError("");
    setLoading(true);
    try {
      const res = await fetch("/api/auth/reset-password", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token, novaSenha: data.novaSenha }),
      });

      if (!res.ok) {
        const problem: ProblemDetails = await res.json();
        setError(problem.title ?? "Erro ao redefinir senha.");
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
      <Box>
        <PageHeader
          title="Senha redefinida!"
          subtitle="Sua senha foi alterada com sucesso. Faça login com a nova senha."
        />
        <Button component={Link} href="/login" variant="contained" color="primary" fullWidth>
          Ir para o login
        </Button>
      </Box>
    );
  }

  return (
    <Box>
      <PageHeader
        title="Redefinir senha"
        subtitle="Crie uma nova senha para sua conta."
      />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <FormProvider {...methods}>
        <Box
          component="form"
          onSubmit={methods.handleSubmit(onSubmit)}
          sx={{ display: "flex", flexDirection: "column", gap: 2 }}
        >
          <PasswordField
            name="novaSenha"
            label="Nova senha"
            required
            autoComplete="new-password"
          />
          <PasswordField
            name="confirmarSenha"
            label="Confirmar nova senha"
            required
            autoComplete="new-password"
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
            Redefinir senha
          </Button>
        </Box>
      </FormProvider>
    </Box>
  );
}

export default function ResetPasswordPage() {
  return (
    <Suspense fallback={<LoadingSpinner />}>
      <ResetPasswordInner />
    </Suspense>
  );
}
