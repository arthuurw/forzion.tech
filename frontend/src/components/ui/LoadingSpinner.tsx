"use client";
import { Box, CircularProgress } from "@mui/material";

interface LoadingSpinnerProps {
  fullPage?: boolean;
  label?: string;
}

export default function LoadingSpinner({ fullPage = false, label = "Carregando" }: LoadingSpinnerProps) {
  if (fullPage) {
    return (
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          minHeight: "100vh",
        }}
      >
        <CircularProgress color="primary" aria-label={label} />
      </Box>
    );
  }

  return (
    <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}>
      <CircularProgress color="primary" aria-label={label} />
    </Box>
  );
}
