import { Box, Typography, Button } from "@mui/material";
import SearchOffIcon from "@mui/icons-material/SearchOff";

export default function NotFound() {
  return (
    <Box
      sx={{
        minHeight: "100vh",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: 2,
        px: 2,
        textAlign: "center",
      }}
    >
      <SearchOffIcon sx={{ fontSize: 64, color: "text.disabled" }} />
      <Typography variant="h4">
        404
      </Typography>
      <Typography variant="h6" color="text.secondary">
        Página não encontrada
      </Typography>
      <Typography variant="body2" color="text.secondary">
        A página que você tentou acessar não está disponível ou foi removida.
      </Typography>
      <Button variant="contained" href="/">
        Voltar ao início
      </Button>
    </Box>
  );
}
