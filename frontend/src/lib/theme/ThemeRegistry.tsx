"use client";
import { ThemeProvider, CssBaseline } from "@mui/material";
import { LocalizationProvider } from "@mui/x-date-pickers/LocalizationProvider";
import { AdapterDayjs } from "@mui/x-date-pickers/AdapterDayjs";
import "dayjs/locale/pt-br";
import theme from "./index";

export default function ThemeRegistry({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      {/* adapterLocale pt-br faz os DatePickers exibirem/parsearem em DD/MM/YYYY
          (o <input type="date"> nativo seguia o locale do SO, gerando MM/DD/YYYY) */}
      <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="pt-br">
        {children}
      </LocalizationProvider>
    </ThemeProvider>
  );
}
