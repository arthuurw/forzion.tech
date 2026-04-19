"use client";
import { Typography, Box } from "@mui/material";
import type { SxProps, Theme } from "@mui/material";

interface LogoProps {
  size?: "sm" | "md" | "lg";
  sx?: SxProps<Theme>;
}

const sizeMap = { sm: "1rem", md: "1.4rem", lg: "2rem" } as const;

export default function Logo({ size = "md", sx }: LogoProps) {
  const fontSize = sizeMap[size];
  return (
    <Box sx={{ display: "flex", alignItems: "baseline", gap: 0.25, ...sx }}>
      <Typography
        component="span"
        sx={{ fontWeight: 700, fontSize, color: "primary.main", lineHeight: 1 }}
      >
        forzion
      </Typography>
      <Typography
        component="span"
        sx={{ fontWeight: 400, fontSize, color: "text.primary", lineHeight: 1 }}
      >
        .tech
      </Typography>
    </Box>
  );
}
