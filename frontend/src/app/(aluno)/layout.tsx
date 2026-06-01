import type { Metadata } from "next";
import AppLayout from "@/components/layout/AppLayout";
import AlunoInadimplenteGate from "@/components/aluno/AlunoInadimplenteGate";

export const metadata: Metadata = {
  robots: { index: false, follow: false },
};

export default function Layout({ children }: { children: React.ReactNode }) {
  return (
    <AppLayout>
      <AlunoInadimplenteGate>{children}</AlunoInadimplenteGate>
    </AppLayout>
  );
}
