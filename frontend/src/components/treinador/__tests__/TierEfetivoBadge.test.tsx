import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import TierEfetivoBadge from "../TierEfetivoBadge";
import type { PlanoPlataformaResponse, TreinadorDashboardPlano } from "@/types";

const PLANO_PRO: PlanoPlataformaResponse = {
  planoId: "plano-pro",
  nome: "Pro",
  tier: "Pro",
  descricao: null,
  maxAlunos: 30,
  preco: 100,
  isAtivo: true,
};

function buildPlano(overrides: Partial<TreinadorDashboardPlano> = {}): TreinadorDashboardPlano {
  return {
    status: "Ativa",
    tierEfetivo: "Pro",
    planoContratadoId: "plano-pro",
    alunosAtivos: 1,
    capEfetivo: 30,
    excedente: 0,
    gracaAte: null,
    temCortesia: false,
    ...overrides,
  };
}

describe("TierEfetivoBadge — nunca afirma 'sem divergência' quando o dado não carregou (G-FE-1)", () => {
  it("planosStatus loading: não mostra badge de divergência nem chip 'confirmado'", () => {
    render(<TierEfetivoBadge plano={buildPlano({ tierEfetivo: "Free" })} planos={[]} planosStatus="loading" />);

    expect(screen.getByText("Free")).toBeInTheDocument();
    expect(screen.queryByText(/pendente de pagamento/)).not.toBeInTheDocument();
  });

  it("planosStatus error: mesma renderização neutra de 'loading', sem afirmar ausência de divergência", () => {
    render(<TierEfetivoBadge plano={buildPlano({ tierEfetivo: "Free" })} planos={[]} planosStatus="error" />);

    expect(screen.getByText("Free")).toBeInTheDocument();
    expect(screen.queryByText(/pendente de pagamento/)).not.toBeInTheDocument();
  });

  it("resolved sem divergência: chip simples do tier efetivo", () => {
    render(
      <TierEfetivoBadge plano={buildPlano({ tierEfetivo: "Pro" })} planos={[PLANO_PRO]} planosStatus="resolved" />,
    );

    expect(screen.getByText("Pro")).toBeInTheDocument();
    expect(screen.queryByText(/pendente de pagamento/)).not.toBeInTheDocument();
  });

  it("resolved com divergência: chip mostra tier efetivo e contratado pendente de pagamento", () => {
    render(
      <TierEfetivoBadge plano={buildPlano({ tierEfetivo: "Free" })} planos={[PLANO_PRO]} planosStatus="resolved" />,
    );

    expect(screen.getByText("Free — Pro pendente de pagamento")).toBeInTheDocument();
  });
});
