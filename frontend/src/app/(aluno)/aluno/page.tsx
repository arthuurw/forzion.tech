"use client";
import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { queryKeys } from "@/lib/query/keys";
import dynamic from "next/dynamic";
import {
  Box, Typography, Paper, Stack, Divider, Button,
} from "@mui/material";
import { useTheme } from "@mui/material/styles";
import FitnessCenterIcon from "@mui/icons-material/FitnessCenter";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import Link from "next/link";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import AlertBanner from "@/components/ui/AlertBanner";
import SemVinculoAtivoBanner from "@/components/aluno/SemVinculoAtivoBanner";
import { alunoApi } from "@/lib/api/aluno";
import { OBJETIVO_LABEL } from "@/lib/constants/labels";
import { extractApiError } from "@/lib/api/extractApiError";

const AlunoDashboardCharts = dynamic(
  () => import("./_charts/AlunoDashboardCharts"),
  { ssr: false, loading: () => <LoadingSpinner /> },
);

export default function DashboardAlunoPage() {
  const theme = useTheme();

  const [error, setError] = useState("");
  const { data, isPending, isError, error: queryError } = useQuery({
    queryKey: queryKeys.aluno.dashboard,
    staleTime: 60 * 1000,
    queryFn: () => alunoApi.getDashboard().then((r) => r.data),
  });

  useEffect(() => {
    if (isError) setError(extractApiError(queryError, "Erro ao carregar dados."));
  }, [isError, queryError]);

  if (isPending) return <LoadingSpinner />;

  const totalFichas = data?.totalFichas ?? 0;
  const totalExecucoes = data?.totalExecucoes ?? 0;
  const fichasAtivas = data?.fichasAtivas ?? [];
  const sessoesPorSemana = data?.sessoesPorSemana ?? [];
  const vinculo = data?.vinculo ?? { ativo: true, pendente: false };

  return (
    <Box>
      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <SemVinculoAtivoBanner vinculo={vinculo} />

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2, mb: 4 }}>
        <Paper sx={{ p: { xs: 2, md: 3 }, borderLeft: `4px solid ${theme.palette.success.main}`, borderRadius: 2 }}>
          <Typography variant="h3" sx={{ fontWeight: 800, lineHeight: 1, color: "success.main" }}>
            {totalFichas}
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: 0.5 }}>
            Fichas disponíveis
          </Typography>
        </Paper>
        <Paper sx={{ p: { xs: 2, md: 3 }, borderLeft: `4px solid ${theme.palette.info.main}`, borderRadius: 2 }}>
          <Typography variant="h3" sx={{ fontWeight: 800, lineHeight: 1, color: "info.main" }}>
            {totalExecucoes}
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: 0.5 }}>
            Sessões realizadas
          </Typography>
        </Paper>
      </Box>

      <AlunoDashboardCharts sessoesPorSemana={sessoesPorSemana} />

      <Paper sx={{ p: { xs: 2, md: 3 }, borderRadius: 2 }}>
        <Typography
          variant="overline"
          color="text.disabled"
          sx={{ letterSpacing: 2, fontSize: "0.7rem", display: "block", mb: 1 }}
        >
          FICHAS ATIVAS
        </Typography>

        {fichasAtivas.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
            Nenhuma ficha ativa disponível.
          </Typography>
        ) : (
          <>
            <Stack divider={<Divider />}>
              {fichasAtivas.map((f) => (
                <Box
                  key={f.treinoAlunoId}
                  sx={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    gap: 2,
                    py: 2,
                    flexWrap: "wrap",
                  }}
                >
                  <Box sx={{ minWidth: 0 }}>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {f.nomeTreino}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {OBJETIVO_LABEL[f.objetivo] ?? f.objetivo}
                    </Typography>
                  </Box>
                  <Stack
                    direction="row"
                    spacing={1}
                    sx={{ width: { xs: "100%", sm: "auto" }, "& > a": { flex: { xs: 1, sm: "0 0 auto" } } }}
                  >
                    <Link href={`/aluno/fichas/${f.treinoAlunoId}`} style={{ textDecoration: "none" }}>
                      <Button size="small" variant="contained" startIcon={<FitnessCenterIcon />} sx={{ width: "100%" }}>
                        Ver ficha
                      </Button>
                    </Link>
                    <Link href={`/aluno/fichas/${f.treinoAlunoId}/executar`} style={{ textDecoration: "none" }}>
                      <Button size="small" variant="outlined" startIcon={<PlayArrowIcon />} sx={{ width: "100%" }}>
                        Executar
                      </Button>
                    </Link>
                  </Stack>
                </Box>
              ))}
            </Stack>
            {totalFichas > 5 && (
              <Box sx={{ pt: 2 }}>
                <Link href="/aluno/fichas" style={{ textDecoration: "none" }}>
                  <Button size="small" variant="text">
                    Ver todas ({totalFichas})
                  </Button>
                </Link>
              </Box>
            )}
          </>
        )}
      </Paper>
    </Box>
  );
}
