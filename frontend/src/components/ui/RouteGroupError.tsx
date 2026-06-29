"use client";
import { useEffect } from "react";
import Link from "next/link";
import * as Sentry from "@sentry/nextjs";
import { Box, Typography, Button } from "@mui/material";
import ReportProblemIcon from "@mui/icons-material/ReportProblem";

export interface RouteGroupErrorProps {
  error: Error & { digest?: string };
  reset: () => void;
  homeHref: string;
  homeLabel: string;
  bodyText: string;
}

export default function RouteGroupError({
  error,
  reset,
  homeHref,
  homeLabel,
  bodyText,
}: RouteGroupErrorProps) {
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
      <ReportProblemIcon sx={{ fontSize: 64, color: "error.main" }} />
      <Typography variant="h5">
        Não foi possível carregar esta página
      </Typography>
      <Typography variant="body2" color="text.secondary">
        {bodyText}
      </Typography>
      <Box sx={{ display: "flex", gap: 2 }}>
        <Button variant="outlined" onClick={reset}>
          Tentar novamente
        </Button>
        <Button variant="contained" component={Link} href={homeHref}>
          {homeLabel}
        </Button>
      </Box>
    </Box>
  );
}
