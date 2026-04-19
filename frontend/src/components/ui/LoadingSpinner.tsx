"use client";
import { Box, CircularProgress } from "@mui/material";

interface LoadingSpinnerProps {
  fullPage?: boolean;
}

export default function LoadingSpinner({ fullPage = false }: LoadingSpinnerProps) {
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
        <CircularProgress color="primary" />
      </Box>
    );
  }

  return (
    <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}>
      <CircularProgress color="primary" />
    </Box>
  );
}
