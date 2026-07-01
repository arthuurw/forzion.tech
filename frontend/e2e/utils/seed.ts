import { randomUUID } from "node:crypto";
import type { APIRequestContext } from "@playwright/test";

export interface SeedAlunoInput {
  treinadorId: string;
  pacoteId: string;
  nome: string;
  email: string;
  senha: string;
  telefone?: string;
  diasDisponiveis?: number;
  tempoDisponivelMinutos?: number;
  finalidade?: string;
  nivelCondicionamento?: string;
}

export interface SeededAluno {
  alunoId?: string;
  email: string;
}

export async function seedAluno(
  request: APIRequestContext,
  input: SeedAlunoInput,
): Promise<SeededAluno> {
  const response = await request.post("/api/auth/register/aluno", {
    data: {
      nome: input.nome,
      email: input.email,
      senha: input.senha,
      telefone: input.telefone ?? null,
      treinadorId: input.treinadorId,
      pacoteId: input.pacoteId,
      diasDisponiveis: input.diasDisponiveis ?? null,
      tempoDisponivelMinutos: input.tempoDisponivelMinutos ?? null,
      finalidade: input.finalidade ?? null,
      nivelCondicionamento: input.nivelCondicionamento ?? null,
      focoTreino: null,
      limitacoesFisicas: null,
      doencas: null,
      observacoesAdicionais: null,
      consentimentoDadosSaude: true,
      consentimentoDadosSaudeEm: new Date().toISOString(),
    },
  });

  if (!response.ok()) {
    const body = await response.text();
    throw new Error(`seedAluno falhou: ${response.status()} ${body}`);
  }

  const data = (await response.json()) as { alunoId?: string };
  return { alunoId: data.alunoId, email: input.email };
}

export async function findTestContaIdByEmail(
  request: APIRequestContext,
  email: string,
): Promise<string | null> {
  const response = await request.get("/api/backend/admin/test-data/contas");
  if (!response.ok()) {
    throw new Error(`findTestContaIdByEmail falhou: GET ${response.status()}`);
  }
  const contas = (await response.json()) as Array<{ contaId: string; email: string }>;
  return contas.find((c) => c.email === email)?.contaId ?? null;
}

export async function cleanupContaByEmail(
  request: APIRequestContext,
  email: string,
): Promise<void> {
  const contaId = await findTestContaIdByEmail(request, email);
  if (!contaId) {
    throw new Error(`cleanupContaByEmail: conta de teste ${email} nao encontrada`);
  }
  const response = await request.delete(`/api/backend/admin/test-data/contas/${contaId}`);
  if (!response.ok()) {
    throw new Error(`cleanupContaByEmail: DELETE ${response.status()} para ${email}`);
  }
}

export function makeTestEmail(prefix: string): string {
  return `${prefix}+${Date.now()}@e2e.test`;
}

export function makeTestSenha(): string {
  return `Aa1!${randomUUID()}`;
}
