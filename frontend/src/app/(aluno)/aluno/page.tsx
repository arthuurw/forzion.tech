"use client";
import { useEffect, useState } from "react";
import { Box, Grid, Card, CardContent, Typography, Button } from "@mui/material";
import FitnessCenterIcon from "@mui/icons-material/FitnessCenter";
import HistoryIcon from "@mui/icons-material/History";
import Link from "next/link";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import AlertBanner from "@/components/ui/AlertBanner";
import { useAuth } from "@/lib/auth/context";
import { alunoApi } from "@/lib/api/aluno";

export default function DashboardAlunoPage() {
  const { user } = useAuth();
  const [totalFichas, setTotalFichas] = useState(0);
  const [totalExecucoes, setTotalExecucoes] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const load = async () => {
      try {
        const [fichasRes, execucoesRes] = await Promise.all([
          alunoApi.listFichas({ tamanhoPagina: 1 }),
          alunoApi.listExecucoes({ tamanhoPagina: 1 }),
        ]);
        setTotalFichas(fichasRes.data.total);
        setTotalExecucoes(execucoesRes.data.total);
      } catch {
        setError("Erro ao carregar dados.");
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  if (loading) return <LoadingSpinner />;

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 0.5 }}>
        Bem-vindo!
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Aqui está um resumo dos seus treinos.
      </Typography>

      <AlertBanner open={!!error} message={error} />

      <Grid container spacing={2} sx={{ mb: 4 }}>
        <Grid size={{ xs: 12, sm: 6 }}>
          <Link href="/aluno/fichas" style={{ textDecoration: "none" }}>
            <Card
              variant="outlined"
              sx={{ cursor: "pointer", "&:hover": { borderColor: "primary.main" } }}
            >
              <CardContent sx={{ display: "flex", alignItems: "center", gap: 2 }}>
                <Box sx={{ p: 1.5, borderRadius: 2, bgcolor: "primary.main" + "20" }}>
                  <FitnessCenterIcon sx={{ color: "primary.main", fontSize: 26 }} />
                </Box>
                <Box>
                  <Typography variant="h4" sx={{ fontWeight: 700, lineHeight: 1 }}>
                    {totalFichas}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    Fichas de treino
                  </Typography>
                </Box>
              </CardContent>
            </Card>
          </Link>
        </Grid>
        <Grid size={{ xs: 12, sm: 6 }}>
          <Link href="/aluno/historico" style={{ textDecoration: "none" }}>
            <Card
              variant="outlined"
              sx={{ cursor: "pointer", "&:hover": { borderColor: "primary.main" } }}
            >
              <CardContent sx={{ display: "flex", alignItems: "center", gap: 2 }}>
                <Box sx={{ p: 1.5, borderRadius: 2, bgcolor: "primary.main" + "20" }}>
                  <HistoryIcon sx={{ color: "primary.main", fontSize: 26 }} />
                </Box>
                <Box>
                  <Typography variant="h4" sx={{ fontWeight: 700, lineHeight: 1 }}>
                    {totalExecucoes}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    Treinos realizados
                  </Typography>
                </Box>
              </CardContent>
            </Card>
          </Link>
        </Grid>
      </Grid>

      <Box sx={{ display: "flex", gap: 2, flexWrap: "wrap" }}>
        <Link href="/aluno/fichas" style={{ textDecoration: "none" }}>
          <Button variant="contained" startIcon={<FitnessCenterIcon />}>
            Ver minhas fichas
          </Button>
        </Link>
        <Link href="/aluno/historico" style={{ textDecoration: "none" }}>
          <Button variant="outlined" startIcon={<HistoryIcon />}>
            Ver histórico
          </Button>
        </Link>
      </Box>
    </Box>
  );
}
