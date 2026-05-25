import type { APIRequestContext } from "@playwright/test";
import { authStatePath } from "../fixtures/auth";
import fs from "node:fs";

/**
 * Helpers de seed/cleanup de dados de teste via backend real.
 *
 * Fase 10a: seed via `/api/auth/register/aluno` (endpoint publico, mesmo do
 * cadastro UI) e cleanup via DELETE admin. Requer storage state admin.
 *
 * Limitacoes:
 * - Endpoints publicos exigem treinadorId + pacoteId validos (caller fornece)
 * - Cleanup precisa do alunoId retornado pelo cadastro (backend retorna?)
 * - Tests devem usar `cleanupAluno(...)` em afterEach ou afterAll
 */

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

/**
 * Cria um aluno via endpoint publico de registro. Retorna { email } sempre;
 * alunoId opcional (depende da resposta do backend).
 *
 * Para cleanup determanistico, prefira que o teste guarde o email e use
 * cleanupAlunoByEmail() via admin.
 */
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
    },
  });

  if (!response.ok()) {
    const body = await response.text();
    throw new Error(`seedAluno falhou: ${response.status()} ${body}`);
  }

  const data = (await response.json()) as { alunoId?: string };
  return { alunoId: data.alunoId, email: input.email };
}

/**
 * Procura aluno por email via admin listAlunos e retorna alunoId, ou null.
 * Usa storage state admin pre-existente.
 */
export async function findAlunoByEmail(
  request: APIRequestContext,
  email: string,
): Promise<string | null> {
  const adminState = authStatePath("admin");
  if (!fs.existsSync(adminState)) {
    throw new Error("findAlunoByEmail requer admin storage state");
  }
  // Lista alunos com nome=email (backend filtra por nome; usar email parcial pode nao bater)
  // Estrategia: paginar curto e procurar pelo email exato.
  const response = await request.get(`/api/backend/admin/alunos?pagina=1&tamanhoPagina=50`);
  if (!response.ok()) return null;
  const data = (await response.json()) as { items: Array<{ alunoId: string; email: string | null }> };
  const match = data.items.find((a) => a.email === email);
  return match?.alunoId ?? null;
}

/**
 * Remove aluno por id via admin DELETE. No-op se id nao existir mais.
 */
export async function cleanupAluno(
  request: APIRequestContext,
  alunoId: string,
): Promise<void> {
  await request.delete(`/api/backend/admin/alunos/${alunoId}`).catch(() => undefined);
}

export async function cleanupAlunoByEmail(
  request: APIRequestContext,
  email: string,
): Promise<void> {
  const id = await findAlunoByEmail(request, email).catch(() => null);
  if (id) await cleanupAluno(request, id);
}

/**
 * Email determanistico para tests — sufixo timestamp evita colisao entre runs.
 */
export function makeTestEmail(prefix: string): string {
  return `${prefix}+${Date.now()}@e2e.test`;
}
