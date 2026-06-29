"use client";
import { Box, Typography, Button } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import Link from "next/link";
import type { ReactNode } from "react";

interface PageHeaderProps {
  title: string;
  subtitle?: string;
  action?: ReactNode;
  as?: "h1" | "h2";
  backHref?: string;
}

export default function PageHeader({
  title,
  subtitle,
  action,
  as = "h1",
  backHref,
}: PageHeaderProps) {
  return (
    <Box sx={{ mb: 3 }}>
      {backHref && (
        <Button
          component={Link}
          href={backHref}
          variant="outlined"
          startIcon={<ArrowBackIcon />}
          sx={{ mb: 2 }}
        >
          Voltar
        </Button>
      )}
      <Box
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          gap: 2,
          flexWrap: "wrap",
        }}
      >
        <Box sx={{ minWidth: 0 }}>
          <Typography variant="h5" component={as}>
            {title}
          </Typography>
          {subtitle && (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
              {subtitle}
            </Typography>
          )}
        </Box>
        {action && <Box>{action}</Box>}
      </Box>
    </Box>
  );
}
