"use client";
import { useState } from "react";
import { Box, Button, Typography } from "@mui/material";

export default function CopiarPixButton({ codigo }: { codigo: string }) {
  const [status, setStatus] = useState<"idle" | "ok" | "erro">("idle");

  const copiar = async () => {
    try {
      await navigator.clipboard.writeText(codigo);
      setStatus("ok");
    } catch {
      setStatus("erro");
    }
  };

  return (
    <Box sx={{ mt: 1 }}>
      <Button size="small" onClick={copiar}>
        Copiar código
      </Button>
      {status === "ok" && (
        <Typography variant="caption" color="success.main" role="status" sx={{ display: "block", mt: 0.5 }}>
          Código copiado!
        </Typography>
      )}
      {status === "erro" && (
        <Typography variant="caption" color="error" role="alert" sx={{ display: "block", mt: 0.5 }}>
          Não foi possível copiar. Selecione e copie o código manualmente.
        </Typography>
      )}
    </Box>
  );
}
