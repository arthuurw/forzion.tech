import { Box, Container, Typography, Button } from "@mui/material";
import Link from "next/link";
import { Forzion, PorQueExistimos } from "@/app/sobre/content";

export default function Sobre() {
  return (
    <Box sx={{ bgcolor: "background.paper", py: { xs: 6, md: 8 } }}>
      <Container maxWidth="md">
        <Box sx={{ textAlign: "center" }}>
          <Typography variant="h4" sx={{ mb: 2 }}>
            Quem está por trás da <Forzion />
          </Typography>
          <Typography variant="body1" color="text.secondary" sx={{ maxWidth: 640, mx: "auto", mb: 4 }}>
            <PorQueExistimos />
          </Typography>
          <Link href="/sobre" style={{ textDecoration: "none" }}>
            <Button variant="contained" color="primary">
              Conhecer a <Forzion />
            </Button>
          </Link>
        </Box>
      </Container>
    </Box>
  );
}
