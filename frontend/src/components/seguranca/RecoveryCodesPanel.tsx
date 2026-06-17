"use client";
import { Box, Stack, Typography, Button, Paper } from "@mui/material";
import DownloadIcon from "@mui/icons-material/Download";
import { downloadBlob } from "@/lib/utils/downloadBlob";

interface RecoveryCodesPanelProps {
  codigos: string[];
  onConcluir: () => void;
}

export default function RecoveryCodesPanel({ codigos, onConcluir }: RecoveryCodesPanelProps) {
  const baixar = () => {
    const conteudo = `forzion.tech — códigos de recuperação\n\n${codigos.join("\n")}\n`;
    downloadBlob(new Blob([conteudo], { type: "text/plain" }), "forzion-codigos-recuperacao.txt");
  };

  return (
    <Stack spacing={2}>
      <Typography variant="body2" color="text.secondary">
        Guarde estes códigos em local seguro. Cada um pode ser usado uma única vez para acessar sua
        conta caso você perca o aplicativo autenticador. Eles não serão exibidos novamente.
      </Typography>
      <Paper variant="outlined" sx={{ p: 2, bgcolor: "background.default" }}>
        <Box
          component="ul"
          aria-label="Códigos de recuperação"
          sx={{
            listStyle: "none", m: 0, p: 0, display: "grid",
            gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 1,
            fontFamily: "monospace", fontSize: "0.95rem", letterSpacing: "0.05em",
          }}
        >
          {codigos.map((c) => (
            <Box component="li" key={c}>{c}</Box>
          ))}
        </Box>
      </Paper>
      <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }}>
        <Button variant="outlined" startIcon={<DownloadIcon />} onClick={baixar}>
          Baixar códigos
        </Button>
        <Button variant="contained" onClick={onConcluir}>
          Já guardei meus códigos
        </Button>
      </Stack>
    </Stack>
  );
}
