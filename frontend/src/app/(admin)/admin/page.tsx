"use client";
import { useEffect, useState } from "react";
import { Box, Grid, Card, CardContent, Typography, Button, Stack, Chip } from "@mui/material";
import PeopleIcon from "@mui/icons-material/People";
import HourglassEmptyIcon from "@mui/icons-material/HourglassEmpty";
import Link from "next/link";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import AlertBanner from "@/components/ui/AlertBanner";
import StatusChip from "@/components/ui/StatusChip";
import { adminApi } from "@/lib/api/admin";
import type { TreinadorResponse } from "@/types";

interface SummaryCard {
  label: string;
  value: number;
  Icon: React.ElementType;
  color: string;
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

  const cards: SummaryCard[] = [
    { label: "Treinadores cadastrados", value: total, Icon: PeopleIcon, color: "primary.main" },
    { label: "Aguardando aprovação", value: pendentes.length, Icon: HourglassEmptyIcon, color: "warning.main" },
  ];

  if (loading) return <LoadingSpinner />;

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 3 }}>
        Painel Admin
      </Typography>

      <AlertBanner open={!!error} message={error} />

      <Grid container spacing={2} sx={{ mb: 4 }}>
        {cards.map(({ label, value, Icon, color }) => (
          <Grid key={label} size={{ xs: 12, sm: 6 }}>
            <Card variant="outlined">
              <CardContent sx={{ display: "flex", alignItems: "center", gap: 2 }}>
                <Box sx={{ p: 1.5, borderRadius: 2, bgcolor: `${color}20` }}>
                  <Icon sx={{ color, fontSize: 28 }} />
                </Box>
                <Box>
                  <Typography variant="h4" sx={{ fontWeight: 700, lineHeight: 1 }}>
                    {value}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    {label}
                  </Typography>
                </Box>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      {pendentes.length > 0 && (
        <Card variant="outlined">
          <CardContent>
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
              <Typography variant="h6" sx={{ fontWeight: 600 }}>
                Pendentes de aprovação
              </Typography>
              <Link href="/admin/treinadores" style={{ textDecoration: "none" }}>
                <Button size="small" variant="outlined">Ver todos</Button>
              </Link>
            </Box>
            <Stack spacing={1}>
              {pendentes.map((t) => (
                <Box
                  key={t.treinadorId}
                  sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", py: 1 }}
                >
                  <Typography variant="body2" sx={{ fontWeight: 500 }}>{t.nome}</Typography>
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
