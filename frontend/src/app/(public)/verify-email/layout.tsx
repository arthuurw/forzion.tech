import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Verificar e-mail",
  robots: { index: false, follow: true },
};

export default function VerifyEmailLayout({ children }: { children: React.ReactNode }) {
  return children;
}
