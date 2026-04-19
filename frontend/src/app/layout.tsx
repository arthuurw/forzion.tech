import type { Metadata } from "next";
import { Roboto } from "next/font/google";
import { AppRouterCacheProvider } from "@mui/material-nextjs/v15-appRouter";
import { AuthProvider } from "@/lib/auth/context";
import ThemeRegistry from "@/lib/theme/ThemeRegistry";
import "@/styles/globals.css";

const roboto = Roboto({
  weight: ["300", "400", "500", "700"],
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
    <html lang="pt-BR" className={roboto.className}>
      <body>
        <AppRouterCacheProvider>
          <ThemeRegistry>
            <AuthProvider>{children}</AuthProvider>
          </ThemeRegistry>
        </AppRouterCacheProvider>
      </body>
    </html>
  );
}
