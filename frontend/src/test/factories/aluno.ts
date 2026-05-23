import { faker } from "@faker-js/faker";
import type { AlunoResponse, AlunoStatus, TempoDisponivel } from "@/types";

/**
 * Factory para AlunoResponse com defaults realistas.
 *
 * Uso:
 *   const aluno = buildAluno();              // dados randomicos
 *   const ativo = buildAluno({ status: "Ativo", nome: "Joao Silva" });
 */
export function buildAluno(overrides: Partial<AlunoResponse> = {}): AlunoResponse {
  const status: AlunoStatus = "Ativo";
  return {
    alunoId: faker.string.uuid(),
    nome: faker.person.fullName(),
    email: faker.internet.email().toLowerCase(),
    telefone: faker.string.numeric(11),
    status,
    contaId: faker.string.uuid(),
    createdAt: faker.date.recent({ days: 30 }).toISOString(),
    updatedAt: null,
    diasDisponiveis: faker.number.int({ min: 1, max: 7 }),
    tempoDisponivelMinutos: "UmaHora" as TempoDisponivel,
    finalidade: null,
    focoTreino: null,
    nivelCondicionamento: null,
    limitacoesFisicas: null,
    doencas: null,
    observacoesAdicionais: null,
    ...overrides,
  };
}
