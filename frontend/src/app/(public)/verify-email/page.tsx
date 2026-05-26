"use client";
import { Box, Typography, Button, CircularProgress } from "@mui/material";
import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import type { ProblemDetails } from "@/types";

type Status = "verifying" | "success" | "error";

export default function VerifyEmailPage() {
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
        <CircularProgress />
        <Typography variant="body2" color="text.secondary">
          Verificando seu e-mail...
        </Typography>
      </Box>
    );
  }

  if (status === "success") {
    return (
      <Box>
        <Typography variant="h5" sx={{ fontWeight: 700, mb: 0.5 }}>
          E-mail verificado!
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
          Sua conta foi ativada. Agora você já pode fazer login.
        </Typography>
        <Button component={Link} href="/login" variant="contained" color="primary" fullWidth>
          Ir para o login
        </Button>
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 0.5 }}>
        Falha na verificação
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        {error}
      </Typography>
      <Button component={Link} href="/login" variant="outlined" fullWidth>
        Voltar para o login
      </Button>
    </Box>
  );
}
