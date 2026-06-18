"use client";
import { useState } from "react";
import { Box, Typography, ButtonBase } from "@mui/material";
import PlayCircleFilledIcon from "@mui/icons-material/PlayCircleFilled";
import MenuBookOutlinedIcon from "@mui/icons-material/MenuBookOutlined";
import { youtubeThumb, youtubeEmbedUrl } from "@/lib/utils/youtube";

interface ExercicioOrientacaoProps {
  nomeExercicio: string;
  comoExecutar?: string | null;
  videoId?: string | null;
}

export default function ExercicioOrientacao({ nomeExercicio, comoExecutar, videoId }: ExercicioOrientacaoProps) {
  const [reproduzindo, setReproduzindo] = useState(false);

  const texto = comoExecutar?.trim();
  const thumb = youtubeThumb(videoId);
  const embed = youtubeEmbedUrl(videoId);

  if (!texto && !embed) return null;

  return (
    <Box sx={{ mb: 2.5 }}>
      {texto && (
        <Box sx={{ display: "flex", alignItems: "flex-start", gap: 1, mb: embed ? 2 : 0 }}>
          <MenuBookOutlinedIcon sx={{ fontSize: 18, color: "text.secondary", mt: "2px", flexShrink: 0 }} />
          <Box>
            <Typography variant="overline" color="text.secondary" sx={{ display: "block", lineHeight: 1.4 }}>
              Como executar
            </Typography>
            <Typography variant="body2" sx={{ whiteSpace: "pre-line", lineHeight: 1.6 }}>
              {texto}
            </Typography>
          </Box>
        </Box>
      )}

      {embed && (
        <Box sx={{ position: "relative", width: "100%", aspectRatio: "16 / 9", borderRadius: 2, overflow: "hidden", bgcolor: "grey.900" }}>
          {reproduzindo ? (
            <Box
              component="iframe"
              src={embed}
              title={`Vídeo de execução: ${nomeExercicio}`}
              allow="accelerometer; encrypted-media; gyroscope; picture-in-picture"
              allowFullScreen
              referrerPolicy="strict-origin-when-cross-origin"
              sx={{ position: "absolute", inset: 0, width: "100%", height: "100%", border: 0 }}
            />
          ) : (
            <ButtonBase
              onClick={() => setReproduzindo(true)}
              aria-label={`Assistir vídeo de execução: ${nomeExercicio}`}
              sx={{ position: "absolute", inset: 0, width: "100%", height: "100%", display: "block" }}
            >
              {thumb && (
                <Box
                  component="img"
                  src={thumb}
                  alt=""
                  loading="lazy"
                  sx={{ position: "absolute", inset: 0, width: "100%", height: "100%", objectFit: "cover" }}
                />
              )}
              <Box sx={{ position: "absolute", inset: 0, display: "flex", alignItems: "center", justifyContent: "center", bgcolor: "rgba(0,0,0,0.25)" }}>
                <PlayCircleFilledIcon sx={{ fontSize: 64, color: "rgba(255,255,255,0.92)" }} />
              </Box>
            </ButtonBase>
          )}
        </Box>
      )}
    </Box>
  );
}
