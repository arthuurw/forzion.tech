"use client";
import { Box, Typography, Button, CircularProgress } from "@mui/material";
import { useEffect, useRef, useState, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import PageHeader from "@/components/ui/PageHeader";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import type { ProblemDetails } from "@/types";

type Status = "verifying" | "success" | "error";

function VerifyEmailInner() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";

  const [status, setStatus] = useState<Status>(token ? "verifying" : "error");
  const [error, setError] = useState(token ? "" : "Link de verificação inválido ou incompleto.");
  const jaChamou = useRef(false);

  useEffect(() => {
    if (!token || jaChamou.current) return;
    jaChamou.current = true;

    (async () => {
      try {
        const res = await fetch("/api/auth/verify-email", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ token }),
        });

        if (!res.ok) {
          const problem: ProblemDetails = await res.json();
          setError(problem.detail ?? problem.title ?? "Não foi possível verificar o e-mail.");
          setStatus("error");
          return;
        }

        setStatus("success");
      } catch {
        setError("Não foi possível conectar ao servidor.");
        setStatus("error");
      }
    })();
  }, [token]);

  if (status === "verifying") {
    return (
      <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 2, py: 2 }}>
        <CircularProgress aria-label="Verificando seu e-mail" />
        <Typography variant="body2" color="text.secondary">
          Verificando seu e-mail...
        </Typography>
      </Box>
    );
  }

  if (status === "success") {
    return (
      <Box>
        <PageHeader
          title="E-mail verificado!"
          subtitle="Sua conta foi ativada. Agora você já pode fazer login."
        />
        <Button component={Link} href="/login" variant="contained" color="primary" fullWidth>
          Ir para o login
        </Button>
      </Box>
    );
  }

  return (
    <Box>
      <PageHeader title="Falha na verificação" subtitle={error} />
      <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
        <Button
          component={Link}
          href="/resend-verification"
          variant="contained"
          color="primary"
          fullWidth
        >
          Reenviar verificação
        </Button>
        <Button component={Link} href="/login" variant="outlined" fullWidth>
          Voltar para o login
        </Button>
      </Box>
    </Box>
  );
}

export default function VerifyEmailPage() {
  return (
    <Suspense fallback={<LoadingSpinner />}>
      <VerifyEmailInner />
    </Suspense>
  );
}
