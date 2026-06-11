import { faker } from "@faker-js/faker";
import type { LoginResponse, SessionUser, TipoConta } from "@/types";

/**
 * Factory para LoginResponse (payload de autenticacao da conta).
 *
 * Uso:
 *   const conta = buildConta();
 *   const admin = buildConta({ tipoConta: "SystemAdmin" });
 */
export function buildConta(overrides: Partial<LoginResponse> = {}): LoginResponse {
  const tipoConta: TipoConta = "Aluno";
  return {
    token: faker.string.alphanumeric(64),
    tipoConta,
    contaId: faker.string.uuid(),
    perfilId: faker.string.uuid(),
    nome: faker.person.fullName(),
    ...overrides,
  };
}

/**
 * Factory para SessionUser — subset seguro de sessao (sem token).
 *
 * Uso:
 *   const user = buildSessionUser();
 *   const treinador = buildSessionUser({ tipoConta: "Treinador" });
 */
export function buildSessionUser(overrides: Partial<SessionUser> = {}): SessionUser {
  const tipoConta: TipoConta = "Aluno";
  return {
    contaId: faker.string.uuid(),
    tipoConta,
    perfilId: faker.string.uuid(),
    nome: faker.person.fullName(),
    ...overrides,
  };
}
