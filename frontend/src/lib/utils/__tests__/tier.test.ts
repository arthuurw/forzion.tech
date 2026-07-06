import { describe, it, expect } from "vitest";
import { resolverTierEfetivoInfo, TIER_LABEL } from "@/lib/utils/tier";
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

describe("resolverTierEfetivoInfo (G-FE-1: distingue falha/loading de 'sem divergência')", () => {
  it("planosStatus loading: status 'loading', tierContratado null, nunca divergente=true por falta de dado", () => {
    const info = resolverTierEfetivoInfo(buildPlano(), [PLANO_PRO], "loading");
    expect(info.status).toBe("loading");
    expect(info.tierContratado).toBeNull();
    expect(info.divergente).toBe(false);
  });

  it("planosStatus error: status 'error', tierContratado null, não afirma 'sem divergência'", () => {
    const info = resolverTierEfetivoInfo(buildPlano(), [], "error");
    expect(info.status).toBe("error");
    expect(info.tierContratado).toBeNull();
    expect(info.divergente).toBe(false);
  });

  it("resolved + planoContratadoId não encontrado na lista: tierContratado null, sem divergência", () => {
    const info = resolverTierEfetivoInfo(buildPlano({ planoContratadoId: "inexistente" }), [PLANO_PRO], "resolved");
    expect(info.status).toBe("resolved");
    expect(info.tierContratado).toBeNull();
    expect(info.divergente).toBe(false);
  });

  it("resolved + tier efetivo igual ao contratado: sem divergência", () => {
    const info = resolverTierEfetivoInfo(buildPlano({ tierEfetivo: "Pro" }), [PLANO_PRO], "resolved");
    expect(info.divergente).toBe(false);
    expect(info.tierContratado).toBe("Pro");
  });

  it("resolved + tier efetivo diferente do contratado: divergente=true", () => {
    const info = resolverTierEfetivoInfo(buildPlano({ tierEfetivo: "Free" }), [PLANO_PRO], "resolved");
    expect(info.status).toBe("resolved");
    expect(info.tierContratado).toBe("Pro");
    expect(info.divergente).toBe(true);
  });
});

describe("TIER_LABEL — fonte canônica única (G-FE-2)", () => {
  it("ProPlus usa a copy 'Pro Plus' (mesma copy exibida a admins no catálogo)", () => {
    expect(TIER_LABEL.ProPlus).toBe("Pro Plus");
  });
});
