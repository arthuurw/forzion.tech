"use client";
import { useEffect, useState } from "react";
import { Box, Grid, Card, CardContent, Typography, Button, Stack, Divider } from "@mui/material";
import PeopleIcon from "@mui/icons-material/People";
import HourglassEmptyIcon from "@mui/icons-material/HourglassEmpty";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import Link from "next/link";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import AlertBanner from "@/components/ui/AlertBanner";
import StatusChip from "@/components/ui/StatusChip";
import { adminApi } from "@/lib/api/admin";
import type { TreinadorResponse } from "@/types";

interface MetricCard {
  label: string;
  value: number;
  Icon: React.ElementType;
  accent: string;
  bg: string;
}

export default function DashboardAdminPage() {
  const [pendentes, setPendentes] = useState<TreinadorResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const load = async () => {
      try {
        const [allRes, pendRes] = await Promise.all([
          adminApi.listTreinadores({ tamanhoPagina: 1 }),
          adminApi.listTreinadores({ status: "AguardandoAprovacao", tamanhoPagina: 5 }),
        ]);
        setTotal(allRes.data.total);
        setPendentes(pendRes.data.items);
      } catch {
        setError("Erro ao carregar dados do painel.");
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  const cards: MetricCard[] = [
    { label: "Treinadores cadastrados", value: total, Icon: PeopleIcon, accent: "#1A1A1A", bg: "rgba(26,26,26,0.06)" },
    { label: "Aguardando validação", value: pendentes.length, Icon: HourglassEmptyIcon, accent: "#C9A000", bg: "rgba(245,196,0,0.12)" },
  ];

  if (loading) return <LoadingSpinner />;

  return (
    <Box>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: 700 }}>Painel de controle</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
          Visão consolidada da plataforma
        </Typography>
      </Box>

      <AlertBanner open={!!error} message={error} />

      <Grid container spacing={2.5} sx={{ mb: 4 }}>
        {cards.map(({ label, value, Icon, accent, bg }) => (
          <Grid key={label} size={{ xs: 12, sm: 6 }}>
            <Card sx={{ border: "1px solid", borderColor: "divider" }}>
              <CardContent sx={{ display: "flex", alignItems: "center", gap: 2.5, p: 3, "&:last-child": { pb: 3 } }}>
                <Box
                  sx={{
                    width: 52,
                    height: 52,
                    borderRadius: 3,
                    bgcolor: bg,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    flexShrink: 0,
                  }}
                >
                  <Icon sx={{ color: accent, fontSize: 26 }} />
                </Box>
                <Box>
                  <Typography variant="h3" sx={{ fontWeight: 800, lineHeight: 1, mb: 0.5 }}>
                    {value}
                  </Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ fontWeight: 500 }}>
                    {label}
                  </Typography>
                </Box>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      {pendentes.length > 0 && (
        <Card sx={{ border: "1px solid", borderColor: "divider" }}>
          <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 3 }}>
              <Box>
                <Typography variant="h6" sx={{ fontWeight: 700 }}>Aguardando validação</Typography>
                <Typography variant="caption" color="text.secondary">{pendentes.length} cadastro(s) pendente(s) de aprovação</Typography>
              </Box>
              <Link href="/admin/treinadores" style={{ textDecoration: "none" }}>
                <Button size="small" variant="outlined" endIcon={<ArrowForwardIcon />}>
                  Ver todos
                </Button>
              </Link>
            </Box>
            <Stack divider={<Divider />}>
              {pendentes.map((t) => (
                <Box
                  key={t.treinadorId}
                  sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", py: 1.5 }}
                >
                  <Box>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>{t.nome}</Typography>
                    <Typography variant="caption" color="text.secondary">{t.email}</Typography>
                  </Box>
                  <StatusChip status={t.status} />
                </Box>
              ))}
            </Stack>
          </CardContent>
        </Card>
      )}
    </Box>
  );
}
