# Test Remediation — Session Log

> Append-only. Uma entrada por sessão. Sempre topo mais recente.

---

## 2026-05-28 — Fase 1 implementada e fechada

**Sessão:** Mesma do setup inicial, continuação.

**Feito (4 commits em `fix/frontend-dockerfile-npmrc`):**

1. **F38** (`c6e8def`) `test(frontend): drop MSW webhook handler defaults from frontend scope`
   - Removidos defaults `POST /webhooks/stripe` e `POST /webhooks/resend` do `frontend/src/test/msw/handlers/pagamento.ts`.
   - Decisão: webhooks são server-to-server, não pertencem ao MSW do frontend. `onUnhandledRequest: "error"` agora pega chamada acidental.
   - Comment inline explica a omissão.

2. **F13** (`0b2f982`) `test(backend): replace Task.Delay(10) with explicit CreatedAt seed`
   - `SeedTreinoAsync` ganhou parâmetro opcional `createdAt`.
   - `ListarPorTreinadorAsync_OrdenarPorCreatedAt_RetornaOrdenadoDesc` agora seeda dois treinos com timestamps 1s apart, eliminando o `Task.Delay(10)` flaky.
   - Teste é `[Trait("Category","Integration")]`, gated por Docker (CI).

3. **F2** (`9c74d35`) `test(frontend): assert offline UI surfaces error banner`
   - Refeito `frontend/e2e/specs/network/offline.spec.ts`. Agora: online → offline+reload → assert AlertBanner com mensagem exata do `usePaginatedList` ("Erro ao carregar alunos.") via `getByRole("alert")` → online+reload → banner some.
   - Eliminado `.catch(() => null)` silencioso. 3 expects estritos no fluxo.

4. **F1** (`46184d1`) `test(frontend): tighten CSRF spec with strict statuses + positive control`
   - 4 testes negativos com `expect(status).toBe(401)` estrito (era `toBeOneOf([401, 403])`).
   - Cobre vetores: sem creds, Cookie forjado, Authorization forjado, POST cross-origin (não só GET).
   - Adicionado positive control via `useAuthRole("admin")`: mesma URL com sessão real recebe status ≠ 401/403 — distingue "defesa funciona" de "endpoint sempre nega".

**Métricas:**
- Backend: 1115 pass (`Category!=Integration`)
- Frontend vitest: 380 pass (32 suites)
- E2E specs offline + CSRF: typecheck ok; precisam de ambiente Playwright completo (auth state + app rodando) pra execução real

**Decisões:**
- F34 e F35 permaneceram `deferred` (infra-scope / platform-limitation).
- Pre-commit hook funcionando bem após `chore(tests)` cb790df (trait-based filter da sessão anterior).
- Fase 2 entra como próxima ativa.

**Métricas pós-sessão:** Total findings 38 — done 4, deferred 2, pending 32.

**Próximos passos sugeridos (Fase 2):**
- F10 Password reset: precisa ler `forzion.tech.Application/UseCases/Auth/*` pra ver se `SolicitarResetSenhaHandler`/`RedefinirSenhaHandler` já existem. Se sim, escrever tests. Se parcial, escrever tests guiando o que falta.
- F11 Resend webhook idempotência: ler `ProcessarWebhookResendHandler.cs` (Infrastructure) e espelhar pattern do Stripe handler test.
- E2E specs (F9, F14, F15, F19) precisam de seed data dedicado — abrir conversa sobre seed strategy antes.

---

## 2026-05-28 — Setup inicial dos arquivos state

**Sessão:** Pós-review completo da suite de testes (após 14 commits de P0s mergeados em `fix/frontend-dockerfile-npmrc` na sessão anterior).

**Feito:**
- Lido plano `~/.claude/plans/fa-a-um-review-completo-virtual-squirrel.md` (38 findings).
- Criado `docs/test-remediation/`:
  - `README.md` — convenções, status, workflow.
  - `state.md` — dashboard inicial com snapshot 0/38 done + baseline de métricas (1115 backend / 380 frontend tests, MSW 4/4, Pact 1/4 repo-level).
  - `findings.md` — catálogo F1-F38 com status `pending` (exceto F34/F35 marcados `deferred` por serem infra-scope ou platform-limitation).
  - `log.md` — este arquivo.

**Decisões importantes:**
- F34 (Pact broker SPOF) e F35 (memory leak Chromium-only) classificados como `deferred` desde já — out of test-suite scope.
- Fase 1 escolhida como ativa (cleanup falso confiança, leverage mais alto).
- Estado guardado em `docs/` (committed) ao invés de `.specs/` (banido pelo AGENTS.md) ou `specs/` (reservado pra specification-*).

**Próximos passos sugeridos:**
- Abrir Fase 1 com F1 (CSRF spec). Leitura inicial: `frontend/e2e/specs/security/csrf.spec.ts`, `frontend/e2e/fixtures/test-base.ts`, `frontend/playwright.config.ts`.

**Não feito:**
- Nenhuma task de remediação iniciada — apenas state files criados.

**Métricas pós-sessão:** Total findings 38 — pending 36, deferred 2, done 0.
