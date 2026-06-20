import { request, type APIRequestContext } from "@playwright/test";

interface TestConta {
  contaId: string;
  email: string;
  criadaEm?: string;
}

function requireEnv(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} ausente`);
  }
  return value;
}

async function login(ctx: APIRequestContext, email: string, senha: string): Promise<void> {
  const response = await ctx.post("/api/auth", { data: { email, senha } });
  if (!response.ok()) {
    throw new Error(`login admin falhou: ${response.status()}`);
  }
}

async function main(): Promise<void> {
  const baseURL = requireEnv("E2E_BASE_URL");
  const adminEmail = requireEnv("E2E_ADMIN_EMAIL");
  const adminPassword = requireEnv("E2E_ADMIN_PASSWORD");

  const whitelist = new Set(
    [
      adminEmail,
      process.env.E2E_ALUNO_EMAIL,
      process.env.E2E_TREINADOR_EMAIL,
      process.env.E2E_RESET_EMAIL ?? "user-reset@e2e.test",
    ]
      .filter((email): email is string => !!email)
      .map((email) => email.toLowerCase()),
  );

  const ctx = await request.newContext({ baseURL });
  try {
    await login(ctx, adminEmail, adminPassword);

    const listResponse = await ctx.get("/api/backend/admin/test-data/contas");
    if (!listResponse.ok()) {
      throw new Error(`GET test-data/contas falhou: ${listResponse.status()}`);
    }

    const contas = (await listResponse.json()) as TestConta[];
    const alvos = contas.filter((conta) => !whitelist.has(conta.email.toLowerCase()));
    console.log(`test-data: ${contas.length} conta(s), ${alvos.length} fora da whitelist`);

    const falhas: string[] = [];
    for (const conta of alvos) {
      const deleteResponse = await ctx.delete(
        `/api/backend/admin/test-data/contas/${conta.contaId}`,
      );
      if (deleteResponse.ok()) {
        console.log(`removida: ${conta.email} (${conta.contaId})`);
      } else {
        console.error(`falhou: ${conta.email} (${conta.contaId}) -> ${deleteResponse.status()}`);
        falhas.push(conta.email);
      }
    }

    if (falhas.length > 0) {
      throw new Error(`sweep incompleto: ${falhas.length} falha(s) -> ${falhas.join(", ")}`);
    }
    console.log(`sweep ok: ${alvos.length} removida(s)`);
  } finally {
    await ctx.dispose();
  }
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exit(1);
});
