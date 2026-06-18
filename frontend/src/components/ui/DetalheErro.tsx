"use client";
import { Box, Typography, Button, Stack } from "@mui/material";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlined";

interface DetalheErroProps {
  mensagem: string;
  onRetry?: () => void;
  onVoltar?: () => void;
}

export default function DetalheErro({ mensagem, onRetry, onVoltar }: DetalheErroProps) {
  return (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        gap: 2,
        py: 8,
        textAlign: "center",
        color: "text.secondary",
      }}
    >
      <ErrorOutlineIcon color="error" sx={{ fontSize: 48, opacity: 0.6 }} />
      <Typography variant="body1">{mensagem}</Typography>
      <Stack direction="row" spacing={2}>
        {onRetry && (
          <Button variant="contained" onClick={onRetry}>
            Tentar novamente
          </Button>
        )}
        {onVoltar && (
          <Button variant="outlined" onClick={onVoltar}>
            Voltar
          </Button>
        )}
      </Stack>
    </Box>
  );
}
