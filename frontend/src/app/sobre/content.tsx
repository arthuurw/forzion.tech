import { Box } from "@mui/material";

export function Forzion() {
  return (
    <>
      <Box component="span" sx={{ color: "brand.label" }}>
        forzion
      </Box>
      <Box component="span" sx={{ color: "text.primary" }}>
        .tech
      </Box>
    </>
  );
}

export function PorQueExistimos() {
  return (
    <>
      Treinador perde tempo demais gerenciando aluno por planilha e WhatsApp,
      com ficha que se perde e execução que não é registrada, até tudo virar
      bagunça. A <Forzion /> existe pra trazer organização real a essa
      rotina.
    </>
  );
}
