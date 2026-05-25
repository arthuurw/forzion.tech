import { faker } from "@faker-js/faker";
import type { TreinadorResponse, TreinadorStatus } from "@/types";

export function buildTreinador(
  overrides: Partial<TreinadorResponse> = {},
): TreinadorResponse {
  const status: TreinadorStatus = "Ativo";
  return {
    treinadorId: faker.string.uuid(),
    nome: faker.person.fullName(),
    contaId: faker.string.uuid(),
    status,
    planoPlataformaId: faker.string.uuid(),
    createdAt: faker.date.recent({ days: 60 }).toISOString(),
    ...overrides,
  };
}
