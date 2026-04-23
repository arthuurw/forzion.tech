import type { Metadata } from "next";
import { Inter } from "next/font/google";
import { AppRouterCacheProvider } from "@mui/material-nextjs/v15-appRouter";
import { AuthProvider } from "@/lib/auth/context";
import { SnackbarProvider } from "@/components/ui/SnackbarProvider";
import ThemeRegistry from "@/lib/theme/ThemeRegistry";
import "@/styles/globals.css";

const inter = Inter({
  weight: ["400", "500", "600", "700"],
  subsets: ["latin"],
  display: "swap",
});

export const metadata: Metadata = {
  title: "forzion.tech",
  description: "Plataforma de gestão de treinos para personal trainers",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="pt-BR" className={inter.className}>
      <body>
        <AppRouterCacheProvider>
          <ThemeRegistry>
            <AuthProvider>
              <SnackbarProvider>{children}</SnackbarProvider>
            </AuthProvider>
          </ThemeRegistry>
        </AppRouterCacheProvider>
      </body>
    </html>
  );
}
