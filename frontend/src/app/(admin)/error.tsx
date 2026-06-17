"use client";
import { useEffect } from "react";
import * as Sentry from "@sentry/nextjs";
import { Box, Typography, Button } from "@mui/material";
import ErrorOutlineIcon from "@mui/icons-material/ReportProblem";

export default function AdminError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    Sentry.captureException(error);
  }, [error]);

  return (
    <Box
      sx={{
        minHeight: "60vh",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: 2,
        px: 2,
        textAlign: "center",
      }}
    >
      <ErrorOutlineIcon sx={{ fontSize: 64, color: "error.main" }} />
      <Typography variant="h5" sx={{ fontWeight: 700 }}>
        Não foi possível carregar esta página
      </Typography>
      <Typography variant="body2" color="text.secondary">
        Um erro inesperado ocorreu. Se o problema persistir, volte ao painel.
      </Typography>
      <Box sx={{ display: "flex", gap: 2 }}>
        <Button variant="outlined" onClick={reset}>
          Tentar novamente
        </Button>
        <Button variant="contained" href="/admin">
          Painel
        </Button>
      </Box>
    </Box>
  );
}
