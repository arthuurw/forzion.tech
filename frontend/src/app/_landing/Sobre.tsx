import { Box, Container, Typography, Button } from "@mui/material";
import Link from "next/link";
import { POR_QUE_EXISTIMOS } from "@/app/sobre/content";

export default function Sobre() {
  return (
    <Box sx={{ bgcolor: "background.paper", py: { xs: 6, md: 8 } }}>
      <Container maxWidth="md">
        <Box sx={{ textAlign: "center" }}>
          <Typography variant="h4" sx={{ mb: 2 }}>
            Quem está por trás da FORZION.TECH
          </Typography>
          <Typography variant="body1" color="text.secondary" sx={{ maxWidth: 640, mx: "auto", mb: 4 }}>
            {POR_QUE_EXISTIMOS}
          </Typography>
          <Link href="/sobre" style={{ textDecoration: "none" }}>
            <Button variant="outlined" color="secondary">
              Conhecer a FORZION.TECH
            </Button>
          </Link>
        </Box>
      </Container>
    </Box>
  );
}
