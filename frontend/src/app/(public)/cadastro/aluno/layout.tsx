import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Criar conta — Aluno",
  alternates: { canonical: "/cadastro/aluno" },
  robots: { index: true, follow: true },
};

export default function CadastroAlunoLayout({ children }: { children: React.ReactNode }) {
  return children;
}
