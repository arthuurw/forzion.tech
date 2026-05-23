import { adminHandlers } from "./admin";
import { alunoHandlers } from "./aluno";
import { treinadorHandlers } from "./treinador";
import { pagamentoHandlers } from "./pagamento";
import { authHandlers } from "./auth";

export const handlers = [
  ...adminHandlers,
  ...alunoHandlers,
  ...treinadorHandlers,
  ...pagamentoHandlers,
  ...authHandlers,
];

export {
  adminHandlers,
  alunoHandlers,
  treinadorHandlers,
  pagamentoHandlers,
  authHandlers,
};
