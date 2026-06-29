import { Box, Container, Typography, Grid, Card, CardContent, Skeleton } from "@mui/material";

export default function PlanosSkeleton() {
  return (
    <Box sx={{ bgcolor: "secondary.main", py: { xs: 6, md: 8 } }} aria-busy="true" aria-label="Carregando planos">
      <Container maxWidth="md">
        <Box sx={{ textAlign: "center", mb: 6 }}>
          <Typography variant="h4" sx={{ mb: 1, color: "white" }}>
            Planos para cada porte de operação
          </Typography>
          <Typography variant="body1" sx={{ color: "rgba(255,255,255,0.72)" }}>
            Escale conforme sua carteira de alunos cresce
          </Typography>
        </Box>
        <Grid container spacing={3} sx={{ justifyContent: "center" }}>
          {[0, 1, 2].map((i) => (
            <Grid key={i} size={{ xs: 12, sm: 6, md: 4 }} sx={{ display: "flex" }}>
              <Card
                sx={{
                  textAlign: "center",
                  p: 1,
                  height: "100%",
                  width: "100%",
                  display: "flex",
                  flexDirection: "column",
                  bgcolor: "rgba(255,255,255,0.06)",
                  border: "1px solid",
                  borderColor: "rgba(255,255,255,0.1)",
                }}
              >
                <CardContent sx={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 1.5 }}>
                  <Skeleton variant="text" width="55%" height={32} sx={{ bgcolor: "rgba(255,255,255,0.12)" }} />
                  <Skeleton variant="text" width="40%" height={20} sx={{ bgcolor: "rgba(255,255,255,0.12)" }} />
                  <Skeleton variant="text" width="50%" height={40} sx={{ bgcolor: "rgba(255,255,255,0.12)" }} />
                  <Skeleton variant="text" width="70%" height={20} sx={{ bgcolor: "rgba(255,255,255,0.12)" }} />
                </CardContent>
              </Card>
            </Grid>
          ))}
        </Grid>
      </Container>
    </Box>
  );
}
