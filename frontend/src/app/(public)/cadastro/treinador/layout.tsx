import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Criar conta — Treinador",
  alternates: { canonical: "/cadastro/treinador" },
  robots: { index: true, follow: true },
};

export default function CadastroTreinadorLayout({ children }: { children: React.ReactNode }) {
  return children;
}
