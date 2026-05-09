"use client";
import { createTheme, alpha } from "@mui/material/styles";
import { ptBR } from "@mui/material/locale";

const theme = createTheme(
  {
    palette: {
      primary: {
        main: "#F5C400",
        light: "#FFD740",
        dark: "#C9A000",
        contrastText: "#1A1A1A",
      },
      secondary: {
        main: "#1A1A1A",
        light: "#2E2E2E",
        contrastText: "#FFFFFF",
      },
      error: {
        main: "#D32F2F",
      },
      background: {
        default: "#F7F8FA",
        paper: "#FFFFFF",
      },
      text: {
        primary: "#111827",
        secondary: "#6B7280",
      },
      divider: "rgba(0,0,0,0.08)",
    },
    typography: {
      fontFamily: "'Inter', 'Roboto', sans-serif",
      h1: { fontWeight: 700, letterSpacing: "-0.02em" },
      h2: { fontWeight: 700, letterSpacing: "-0.015em" },
      h3: { fontWeight: 700, letterSpacing: "-0.01em" },
      h4: { fontWeight: 700, letterSpacing: "-0.01em" },
      h5: { fontWeight: 600, letterSpacing: "-0.005em" },
      h6: { fontWeight: 600 },
      subtitle1: { fontWeight: 500 },
      button: { textTransform: "none", fontWeight: 600, letterSpacing: 0 },
    },
    shape: {
      borderRadius: 12,
    },
    shadows: [
      "none",
      "0 1px 2px rgba(0,0,0,0.06), 0 1px 3px rgba(0,0,0,0.08)",
      "0 2px 4px rgba(0,0,0,0.06), 0 1px 6px rgba(0,0,0,0.08)",
      "0 4px 8px rgba(0,0,0,0.06), 0 2px 8px rgba(0,0,0,0.08)",
      "0 4px 16px rgba(0,0,0,0.08), 0 2px 6px rgba(0,0,0,0.06)",
      "0 8px 24px rgba(0,0,0,0.1), 0 2px 8px rgba(0,0,0,0.06)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
    ],
    components: {
      MuiButton: {
        defaultProps: { disableElevation: true },
        styleOverrides: {
          root: {
            borderRadius: 10,
            paddingTop: 10,
            paddingBottom: 10,
            transition: "all 0.15s ease",
            "&.MuiButton-containedPrimary:hover": { backgroundColor: "#E6B800" },
            "&.MuiButton-outlinedSecondary": {
              borderColor: alpha("#1A1A1A", 0.3),
              "&:hover": { borderColor: "#1A1A1A", backgroundColor: alpha("#1A1A1A", 0.04) },
            },
          },
          sizeLarge: {
            paddingTop: 13,
            paddingBottom: 13,
            paddingLeft: 28,
            paddingRight: 28,
            fontSize: "1rem",
          },
        },
      },
      MuiTextField: {
        defaultProps: { variant: "outlined", size: "small", fullWidth: true },
      },
      MuiOutlinedInput: {
        styleOverrides: {
          root: {
            borderRadius: 10,
            backgroundColor: "#fff",
            "& fieldset": { borderColor: "rgba(0,0,0,0.15)" },
            "&:hover fieldset": { borderColor: "rgba(0,0,0,0.4)" },
          },
        },
      },
      MuiCard: {
        styleOverrides: {
          root: {
            borderRadius: 16,
            boxShadow: "0 1px 3px rgba(0,0,0,0.06), 0 1px 8px rgba(0,0,0,0.06)",
          },
        },
      },
      MuiPaper: {
        styleOverrides: {
          rounded: { borderRadius: 16 },
        },
      },
      MuiChip: {
        styleOverrides: {
          root: { fontWeight: 600, borderRadius: 8 },
        },
      },
      MuiListItemButton: {
        styleOverrides: {
          root: {
            borderRadius: 10,
            transition: "all 0.15s ease",
          },
        },
      },
      MuiAppBar: {
        styleOverrides: {
          root: { backgroundImage: "none" },
        },
      },
      MuiDrawer: {
        styleOverrides: {
          paper: { backgroundImage: "none" },
        },
      },
      MuiDivider: {
        styleOverrides: {
          root: { borderColor: "rgba(0,0,0,0.07)" },
        },
      },
    },
  },
  ptBR
);

export default theme;
