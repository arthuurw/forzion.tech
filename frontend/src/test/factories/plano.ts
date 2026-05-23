import { faker } from "@faker-js/faker";
import type { PlanoTreinadorResponse } from "@/types";

export function buildPlano(
  overrides: Partial<PlanoTreinadorResponse> = {},
): PlanoTreinadorResponse {
  return {
    planoId: faker.string.uuid(),
    nome: `Plano ${faker.commerce.productAdjective()}`,
    tier: "Basic",
    descricao: faker.commerce.productDescription(),
    maxAlunos: faker.number.int({ min: 5, max: 100 }),
    preco: faker.number.float({ min: 49.9, max: 499.9, fractionDigits: 2 }),
    isAtivo: true,
    createdAt: faker.date.recent({ days: 90 }).toISOString(),
    updatedAt: null,
    ...overrides,
  };
}
