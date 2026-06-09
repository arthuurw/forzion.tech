import { Box, Typography } from "@mui/material";

// variant = cor do FUNDO da seção onde o eyebrow vive. Amarelo brilhante só passa AA sobre
// superfície escura (theme brand.label gotcha F18) — por isso em seção clara a pill recebe
// fundo preto sólido, e em seção escura usa a pill translúcida do Hero.
const VARIANTS = {
  light: { bgcolor: "secondary.main", borderColor: "primary.main" },
  dark: { bgcolor: "rgba(245,196,0,0.08)", borderColor: "rgba(245,196,0,0.3)" },
} as const;

export default function SectionEyebrow({
  label,
  variant,
}: {
  label: string;
  variant: keyof typeof VARIANTS;
}) {
  return (
    <Box
      sx={{
        display: "inline-flex",
        alignItems: "center",
        gap: 1,
        px: 2,
        py: 0.75,
        borderRadius: 10,
        border: "1px solid",
        ...VARIANTS[variant],
      }}
    >
      <Box sx={{ width: 7, height: 7, borderRadius: "50%", bgcolor: "primary.main" }} />
      <Typography variant="caption" sx={{ color: "primary.main", fontWeight: 700, letterSpacing: "0.1em" }}>
        {label}
      </Typography>
    </Box>
  );
}
