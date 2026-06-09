"use client";
import { Box, Container, Typography, Grid, Avatar } from "@mui/material";

export interface Testimonial {
  id?: string;
  text: string;
  name: string;
  city: string;
  photo?: string;
}

interface SocialProofProps {
  testimonials: Testimonial[];
  count?: number;
}

// Retorna null quando vazio: decisão deliberada para o beta — sem depoimentos
// reais ainda, fabricar conteúdo prejudicaria a credibilidade do produto.
export default function SocialProof({ testimonials, count }: SocialProofProps) {
  if (testimonials.length === 0 && !count) return null;

  return (
    <Box sx={{ bgcolor: "background.paper", py: { xs: 8, md: 12 } }}>
      <Container maxWidth="md">
        <Box sx={{ textAlign: "center", mb: 8 }}>
          <Typography variant="overline" sx={{ color: "#7a6300", fontWeight: 700, letterSpacing: "0.1em" }}>
            DEPOIMENTOS
          </Typography>
          <Typography variant="h4" sx={{ fontWeight: 700, mt: 1 }}>
            O que dizem nossos treinadores
          </Typography>
          {count !== undefined && count > 0 && (
            <Typography variant="subtitle1" sx={{ mt: 2, color: "text.secondary" }}>
              +{count} treinadores cadastrados
            </Typography>
          )}
        </Box>
        {testimonials.length > 0 && (
          <Grid container spacing={4}>
            {testimonials.map(({ text, name, city, photo }) => (
              <Grid key={`${name}-${city}`} size={{ xs: 12, md: 4 }}>
                <Box
                  sx={{
                    bgcolor: "background.default",
                    borderRadius: 4,
                    p: 3.5,
                    height: "100%",
                    border: "1px solid",
                    borderColor: "divider",
                  }}
                >
                  <Typography variant="body2" color="text.secondary" sx={{ lineHeight: 1.7, mb: 3 }}>
                    &quot;{text}&quot;
                  </Typography>
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
                    <Avatar src={photo} alt={name} sx={{ width: 40, height: 40 }} />
                    <Box>
                      <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                        {name}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {city}
                      </Typography>
                    </Box>
                  </Box>
                </Box>
              </Grid>
            ))}
          </Grid>
        )}
      </Container>
    </Box>
  );
}
