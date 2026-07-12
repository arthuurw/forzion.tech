"use client";
import { Box, Button, CircularProgress, Typography } from "@mui/material";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useState, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import PasswordField from "@/components/forms/PasswordField";
import FormTextField from "@/components/forms/FormTextField";
import AlertBanner from "@/components/ui/AlertBanner";
import PageHeader from "@/components/ui/PageHeader";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import { registerPasswordSchema } from "@/lib/validations/common";
import { extractApiErrorInfo } from "@/lib/api/extractApiError";

const schema = z
  .object({
    novaSenha: registerPasswordSchema,
    confirmarSenha: z.string().min(1, "Confirmação obrigatória"),
    codigoTotp: z.string().optional(),
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
  const [mfaRequerido, setMfaRequerido] = useState(false);
  const [bloqueado, setBloqueado] = useState(false);

  const methods = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { novaSenha: "", confirmarSenha: "", codigoTotp: "" },
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
    if (mfaRequerido && !data.codigoTotp?.trim()) {
      methods.setError("codigoTotp", { message: "Informe o código de verificação." });
      return;
    }
    setLoading(true);
    try {
      const body: Record<string, string> = { token, novaSenha: data.novaSenha };
      if (mfaRequerido && data.codigoTotp?.trim()) body.codigoTotp = data.codigoTotp.trim();

      const res = await fetch("/api/auth/reset-password", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });

      if (!res.ok) {
        let problem: unknown;
        try {
          problem = await res.json();
        } catch {
          setError("Erro ao redefinir senha.");
          return;
        }
        const { code, message } = extractApiErrorInfo({ response: { data: problem, status: res.status } });

        if (code === "mfa.codigo_invalido") {
          if (mfaRequerido) {
            methods.setError("codigoTotp", { message: "Código inválido." });
          } else {
            setMfaRequerido(true);
            setError("Esta conta usa verificação em duas etapas. Informe o código do seu aplicativo autenticador.");
          }
          return;
        }
        if (code === "auth_reset.segundo_fator_bloqueado") {
          setBloqueado(true);
          setError(message ?? "Muitas tentativas. Solicite um novo link de redefinição.");
          return;
        }
        setError(message ?? "Erro ao redefinir senha.");
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
          {mfaRequerido && (
            <>
              <Typography variant="body2" color="text.secondary">
                Esta conta usa verificação em duas etapas. Informe o código do seu aplicativo autenticador.
              </Typography>
              <FormTextField
                name="codigoTotp"
                label="Código de verificação"
                slotProps={{ htmlInput: { inputMode: "numeric", autoComplete: "one-time-code" } }}
                fullWidth
              />
            </>
          )}
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
          {bloqueado && (
            <Button component={Link} href="/forgot-password" variant="outlined" fullWidth>
              Solicitar novo link
            </Button>
          )}
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
