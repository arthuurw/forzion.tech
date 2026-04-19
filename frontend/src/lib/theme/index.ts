"use client";
import { createTheme } from "@mui/material/styles";
import { ptBR } from "@mui/material/locale";

const theme = createTheme(
  {
    palette: {
      primary: {
        main: "#F5C400",
        contrastText: "#1A1A1A",
      },
      secondary: {
        main: "#1A1A1A",
        contrastText: "#FFFFFF",
      },
      error: {
        main: "#D32F2F",
      },
      background: {
        default: "#F5F5F5",
        paper: "#FFFFFF",
      },
      text: {
        primary: "#1A1A1A",
        secondary: "#5C5C5C",
      },
    },
    typography: {
      fontFamily: "Roboto, sans-serif",
      h1: { fontWeight: 700 },
      h2: { fontWeight: 700 },
      h3: { fontWeight: 600 },
      h4: { fontWeight: 600 },
      h5: { fontWeight: 500 },
      h6: { fontWeight: 500 },
      button: { textTransform: "none", fontWeight: 500 },
    },
    shape: {
      borderRadius: 8,
    },
    components: {
      MuiButton: {
        defaultProps: { disableElevation: true },
        styleOverrides: {
          root: { borderRadius: 8, paddingTop: 10, paddingBottom: 10 },
        },
      },
      MuiTextField: {
        defaultProps: { variant: "outlined", size: "small", fullWidth: true },
      },
      MuiCard: {
        styleOverrides: {
          root: { boxShadow: "0 1px 4px rgba(0,0,0,0.08)" },
        },
      },
      MuiChip: {
        styleOverrides: {
          root: { fontWeight: 500 },
        },
      },
    },
  },
  ptBR
);

export default theme;
