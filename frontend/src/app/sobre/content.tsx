import { Box } from "@mui/material";

interface ForzionProps {
  inherit?: boolean;
}

export function Forzion({ inherit = false }: ForzionProps) {
  return (
    <>
      <Box component="span" sx={{ color: inherit ? "inherit" : "brand.label" }}>
        forzion
      </Box>
      <Box component="span" sx={{ color: inherit ? "inherit" : "text.primary" }}>
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
