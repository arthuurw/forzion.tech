"use client";
import { Box, Typography, useMediaQuery, useTheme } from "@mui/material";
import Logo from "@/components/ui/Logo";
import FitnessCenterIcon from "@mui/icons-material/FitnessCenter";
import GroupIcon from "@mui/icons-material/Group";
import TrackChangesIcon from "@mui/icons-material/TrackChanges";

const FEATURES = [
  { Icon: FitnessCenterIcon, text: "Prescrições estruturadas por objetivo e nível" },
  { Icon: GroupIcon, text: "Gestão completa da carteira de alunos" },
  { Icon: TrackChangesIcon, text: "Histórico de execuções para análise e ajuste" },
];

export default function PublicLayout({ children }: { children: React.ReactNode }) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));

  if (isMobile) {
    return (
      <Box sx={{ minHeight: "100dvh", bgcolor: "background.default", display: "flex", flexDirection: "column" }}>
        <Box sx={{ px: 3, py: 2.5, borderBottom: "1px solid", borderColor: "divider", bgcolor: "background.paper" }}>
          <Logo size="md" />
        </Box>
        <Box sx={{ flex: 1, display: "flex", alignItems: "center", justifyContent: "center", p: 3 }}>
          <Box sx={{ width: "100%", maxWidth: 420 }}>{children}</Box>
        </Box>
        <Box sx={{ py: 2, textAlign: "center" }}>
          <Typography variant="caption" color="text.secondary">
            © {new Date().getFullYear()} forzion.tech
          </Typography>
        </Box>
      </Box>
    );
  }

  return (
    <Box sx={{ display: "flex", minHeight: "100dvh" }}>
      {/* Painel esquerdo — branding */}
      <Box
        sx={{
          width: { md: "42%", lg: "38%" },
          flexShrink: 0,
          alignSelf: "flex-start",
          position: "sticky",
          top: 0,
          height: "100dvh",
          bgcolor: "secondary.main",
          display: "flex",
          flexDirection: "column",
          justifyContent: "space-between",
          p: { md: 5, lg: 6 },
          overflow: "hidden",
          "&::before": {
            content: '""',
            position: "absolute",
            top: -80,
            right: -80,
            width: 300,
            height: 300,
            borderRadius: "50%",
            bgcolor: "primary.main",
            opacity: 0.07,
          },
          "&::after": {
            content: '""',
            position: "absolute",
            bottom: -60,
            left: -60,
            width: 220,
            height: 220,
            borderRadius: "50%",
            bgcolor: "primary.main",
            opacity: 0.05,
          },
        }}
      >
        <Box>
          <Logo size="lg" sx={{ "& span": { color: "white" }, "& span:first-of-type": { color: "primary.main" } }} />
          <Typography
            variant="h4"
            sx={{ color: "white", fontWeight: 700, mt: 5, mb: 1.5, lineHeight: 1.25 }}
          >
            Gestão profissional para{" "}
            <Box component="span" sx={{ color: "primary.main" }}>
              personal trainers
            </Box>
          </Typography>
          <Typography variant="body1" sx={{ color: "rgba(255,255,255,0.55)", mb: 5, lineHeight: 1.7 }}>
            Estruture sua operação, atenda com mais critério e demonstre resultado com dados.
          </Typography>

          <Box sx={{ display: "flex", flexDirection: "column", gap: 2.5 }}>
            {FEATURES.map(({ Icon, text }) => (
              <Box key={text} sx={{ display: "flex", alignItems: "center", gap: 2 }}>
                <Box
                  sx={{
                    width: 38,
                    height: 38,
                    borderRadius: 2,
                    bgcolor: "primary.main",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    flexShrink: 0,
                  }}
                >
                  <Icon sx={{ fontSize: 20, color: "secondary.main" }} />
                </Box>
                <Typography variant="body2" sx={{ color: "rgba(255,255,255,0.75)", lineHeight: 1.5 }}>
                  {text}
                </Typography>
              </Box>
            ))}
          </Box>
        </Box>

        <Typography variant="caption" sx={{ color: "rgba(255,255,255,0.3)" }}>
          © {new Date().getFullYear()} forzion.tech
        </Typography>
      </Box>

      {/* Painel direito — formulário */}
      <Box
        sx={{
          flex: 1,
          alignSelf: "flex-start",
          minHeight: "100dvh",
          bgcolor: "background.default",
          display: "flex",
          flexDirection: "column",
          p: { md: 5, lg: 8 },
        }}
      >
        <Box
          sx={{
            width: "100%",
            maxWidth: 440,
            mx: "auto",
            my: "auto",
            bgcolor: "background.paper",
            borderRadius: 4,
            p: { md: 4, lg: 5 },
            boxShadow: "0 4px 24px rgba(0,0,0,0.06), 0 1px 4px rgba(0,0,0,0.04)",
          }}
        >
          {children}
        </Box>
      </Box>
    </Box>
  );
}
