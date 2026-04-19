"use client";
import { Box, Container, Typography, Link as MuiLink } from "@mui/material";
import Logo from "@/components/ui/Logo";

export default function PublicLayout({ children }: { children: React.ReactNode }) {
  return (
    <Box sx={{ display: "flex", flexDirection: "column", minHeight: "100vh" }}>
      <Box
        component="header"
        sx={{
          py: 2,
          px: 3,
          display: "flex",
          alignItems: "center",
          borderBottom: "1px solid",
          borderColor: "divider",
          bgcolor: "background.paper",
        }}
      >
        <Logo size="md" />
      </Box>

      <Box component="main" sx={{ flex: 1, py: 4 }}>
        <Container maxWidth="sm">{children}</Container>
      </Box>

      <Box
        component="footer"
        sx={{
          py: 2,
          textAlign: "center",
          borderTop: "1px solid",
          borderColor: "divider",
        }}
      >
        <Typography variant="caption" color="text.secondary">
          © {new Date().getFullYear()} forzion.tech — Todos os direitos reservados
        </Typography>
      </Box>
    </Box>
  );
}
