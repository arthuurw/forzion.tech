import AppLayout from "@/components/layout/AppLayout";
import AlunoInadimplenteGate from "@/components/aluno/AlunoInadimplenteGate";

export default function Layout({ children }: { children: React.ReactNode }) {
  return (
    <AppLayout>
      <AlunoInadimplenteGate>{children}</AlunoInadimplenteGate>
    </AppLayout>
  );
}
