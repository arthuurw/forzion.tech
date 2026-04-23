"use client";
import { useEffect, useState } from "react";
import { Box, Grid, Card, CardContent, Typography, Button, Stack } from "@mui/material";
import PeopleIcon from "@mui/icons-material/People";
import HourglassEmptyIcon from "@mui/icons-material/HourglassEmpty";
import ListAltIcon from "@mui/icons-material/ListAlt";
import Link from "next/link";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import AlertBanner from "@/components/ui/AlertBanner";
import StatusChip from "@/components/ui/StatusChip";
import { treinadorApi } from "@/lib/api/treinador";
import { useAuth } from "@/lib/auth/context";
import type { VinculoDetalheResponse } from "@/types";

export default function DashboardTreinadorPage() {
  const { user } = useAuth();
  const [pendentes, setPendentes] = useState<VinculoDetalheResponse[]>([]);
  const [totalAlunos, setTotalAlunos] = useState(0);
  const [totalFichas, setTotalFichas] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const load = async () => {
      try {
        const [alunosRes, pendRes, fichasRes] = await Promise.all([
          treinadorApi.listAlunos({ status: "Ativo", tamanhoPagina: 1 }),
          treinadorApi.listVinculos({ status: "AguardandoAprovacao", tamanhoPagina: 5 }),
          treinadorApi.listFichas({ tamanhoPagina: 1 }),
        ]);
        setTotalAlunos(alunosRes.data.total);
        setPendentes(pendRes.data.items);
        setTotalFichas(fichasRes.data.total);
      } catch {
        setError("Erro ao carregar dados.");
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  if (loading) return <LoadingSpinner />;

  const cards = [
    { label: "Alunos ativos", value: totalAlunos, Icon: PeopleIcon, href: "/treinador/alunos" },
    { label: "Vínculos pendentes", value: pendentes.length, Icon: HourglassEmptyIcon, href: "/treinador/alunos" },
    { label: "Fichas de treino", value: totalFichas, Icon: ListAltIcon, href: "/treinador/treinos" },
  ];

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 1 }}>
        Painel do treinador
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Visão geral da sua operação
      </Typography>

      <AlertBanner open={!!error} message={error} />

      <Grid container spacing={2} sx={{ mb: 4 }}>
        {cards.map(({ label, value, Icon, href }) => (
          <Grid key={label} size={{ xs: 12, sm: 4 }}>
            <Link href={href} style={{ textDecoration: "none" }}>
              <Card variant="outlined" sx={{ cursor: "pointer", "&:hover": { borderColor: "primary.main" } }}>
                <CardContent sx={{ display: "flex", alignItems: "center", gap: 2 }}>
                  <Box sx={{ p: 1.5, borderRadius: 2, bgcolor: "primary.main" + "20" }}>
                    <Icon sx={{ color: "primary.main", fontSize: 26 }} />
                  </Box>
                  <Box>
                    <Typography variant="h4" sx={{ fontWeight: 700, lineHeight: 1 }}>{value}</Typography>
                    <Typography variant="body2" color="text.secondary">{label}</Typography>
                  </Box>
                </CardContent>
              </Card>
            </Link>
          </Grid>
        ))}
      </Grid>

      {pendentes.length > 0 && (
        <Card variant="outlined">
          <CardContent>
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
              <Typography variant="h6" sx={{ fontWeight: 600 }}>Vínculos aguardando aprovação</Typography>
              <Link href="/treinador/alunos" style={{ textDecoration: "none" }}>
                <Button size="small" variant="outlined">Ver alunos</Button>
              </Link>
            </Box>
            <Stack spacing={1}>
              {pendentes.map((v) => (
                <Box key={v.vinculoId} sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", py: 0.5 }}>
                  <Typography variant="body2" sx={{ fontWeight: 500 }}>{v.nomeAluno}</Typography>
                  <StatusChip status={v.status} />
                </Box>
              ))}
            </Stack>
          </CardContent>
        </Card>
      )}
    </Box>
  );
}
