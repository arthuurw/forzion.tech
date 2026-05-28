# Findings — Status individual (F1-F38)

> Status por finding. Atualizar a cada mudança de estado. Não renumerar.
> Legenda status: `pending` | `in_progress` | `done` | `deferred` | `skipped` | `blocked`

---

## Critical (F1-F12)

### F1 — CSRF spec is no-op assertion
- **Status:** `done`
- **Fase:** 1
- **Arquivo:** `frontend/e2e/specs/security/csrf.spec.ts`
- **Commit:** `46184d1`
- **Data fechado:** 2026-05-28
- **Notas:** Refeito com 4 asserts negativos estritos (status === 401) + positive control via `useAuthRole("admin")`. Cobre vetores: sem creds, Cookie forjado, Authorization forjado, POST cross-origin. Positive control valida que defesa não é só "endpoint sempre nega".

### F2 — Offline spec sem assertion de UI
- **Status:** `done`
- **Fase:** 1
- **Arquivo:** `frontend/e2e/specs/network/offline.spec.ts`
- **Commit:** `9c74d35`
- **Data fechado:** 2026-05-28
- **Notas:** Refeito: online → offline+reload → asserta AlertBanner com "Erro ao carregar alunos." (mensagem exata do `usePaginatedList`) via `getByRole("alert")` → online+reload → banner some. 3 expects estritos no fluxo.

### F3 — Checkout Stripe spec skippa silenciosamente
- **Status:** `pending`
- **Fase:** 4
- **Arquivo:** `frontend/e2e/specs/critical/checkout-stripe.spec.ts:30-34`
- **Commit:** —
- **Data fechado:** —
- **Notas:** `test.skip()` se aluno sem pagamento pendente. CI pode skip todos os runs sem detecção. Seed dedicated user com pagamento garantido + stripe-cli stub pra webhook.

### F4 — Pact provider mocka handlers (3 de 4)
- **Status:** `pending`
- **Fase:** 3
- **Arquivo:** `forzion.tech.PactVerification/ForzionApiProviderTests.cs`
- **Commit (partial):** `d7e395e` (ListarAlunosAdmin já convertido)
- **Data fechado:** —
- **Notas:** Faltam Fichas/Vinculo/Perfil. Aplicar pattern repo-level do ListarAlunos.

### F5 — Pact zero error contracts
- **Status:** `pending`
- **Fase:** 3
- **Arquivo:** `frontend/src/test/pact/consumer.test.ts`
- **Commit:** —
- **Data fechado:** —
- **Notas:** 4 contratos, todos 200. Adicionar 401/404/500 por endpoint.

### F6 — `vi.mock("@/lib/api/*")` ainda ativo em 6 arquivos
- **Status:** `pending`
- **Fase:** 3
- **Arquivos:**
  - `frontend/src/lib/api/admin.test.ts`
  - `frontend/src/components/pagamento/PagamentoCartao.test.tsx`
  - `frontend/src/app/(aluno)/__tests__/pagamento.test.tsx`
  - `frontend/src/app/(admin)/__tests__/admin-pages.test.tsx`
  - `frontend/src/app/(admin)/admin/saude/page.client.test.tsx`
  - (PagamentoCartao listado 2x no review original — confirmar 5 ou 6 únicos)
- **Commit:** —
- **Data fechado:** —
- **Notas:** Migrar pro pattern `admin.msw.test.ts` (axios real + `server.use(...)` override). Bypassa interceptors/retries/error normalization atualmente.

### F7 — `@stripe/react-stripe-js` mockado wholesale
- **Status:** `pending`
- **Fase:** 4
- **Arquivo:** `frontend/src/components/pagamento/PagamentoCartao.test.tsx:11-20`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Manter Elements/PaymentElement mocked (DOM), mas `useStripe`/`useElements` retornarem objetos realistas. Testar error path (card declined → setState).

### F8 — Stryker scope estreito + threshold lax
- **Status:** `pending`
- **Fase:** 3
- **Arquivos:** `stryker-config.json` (backend), `frontend/stryker.conf.json`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Incluir `forzion.tech.Application` no backend (start threshold 60, ratchet). Incluir `src/components/**` no frontend. Rodar mutation em workflow noturno/weekly, não PR gate inicial.

### F9 — Token revocation E2E ausente
- **Status:** `pending`
- **Fase:** 2
- **Novo arquivo:** `frontend/e2e/specs/critical/logout-revokes-jwt.spec.ts`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Login → logout (revoke JTI) → re-tentar JWT antigo → 401. Prova middleware consulta lista de revogados.

### F10 — Password reset flow zero cobertura
- **Status:** `pending`
- **Fase:** 2
- **Novos arquivos:**
  - `forzion.tech.Tests/Application/Auth/SolicitarResetSenha/SolicitarResetSenhaHandlerTests.cs`
  - `forzion.tech.Tests/Application/Auth/RedefinirSenha/RedefinirSenhaHandlerTests.cs`
  - `frontend/e2e/specs/critical/password-reset.spec.ts`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Token gen, expiry, hash storage, replay rejection, password strength. E2E end-to-end.

### F11 — Resend webhook idempotência ausente
- **Status:** `pending`
- **Fase:** 2
- **Novo arquivo:** `forzion.tech.Tests/Application/Pagamentos/ProcessarWebhookResend/ProcessarWebhookResendHandlerTests.cs`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Mirror do Stripe. Event_id dedup, retry-safe, malformed payload.

### F12 — Concurrent billing race não testado
- **Status:** `pending`
- **Fase:** 3
- **Novo arquivo:** `forzion.tech.Tests/E2E/ConcurrentBillingRaceTests.cs`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Sessão anterior (commit `18c3adc`) wrap GerarCobrancaMensal em serializable tx, mas `NoopTransaction` no unit test não prova isolation. Spawning 2 tasks paralelas via Testcontainers, asserting exactly 1 success. Começar quarantined (potencial flaky).

---

## Important (F13-F30)

### F13 — `Task.Delay(10)` flaky em TreinoRepositoryTests
- **Status:** `done`
- **Fase:** 1
- **Arquivo:** `forzion.tech.Tests/Infrastructure/Repositories/TreinoRepositoryTests.cs:122`
- **Commit:** `0b2f982`
- **Data fechado:** 2026-05-28
- **Notas:** `SeedTreinoAsync` ganhou parâmetro opcional `createdAt`. Teste agora seeda dois treinos com timestamps explícitos 1s apart — ordenação determinística sem delay. Categoria Integration (rodado em CI com Docker).

### F14 — Treinador signup E2E ausente
- **Status:** `pending`
- **Fase:** 2
- **Novo arquivo:** `frontend/e2e/specs/critical/treinador-signup.spec.ts`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Form `/cadastro/treinador` + email verification.

### F15 — Email verification E2E ausente
- **Status:** `pending`
- **Fase:** 2
- **Novo arquivo:** `frontend/e2e/specs/critical/email-verification.spec.ts`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Cobre verify-email + resend-verification + F23 replay test.

### F16 — Decimal/currency rounding sem property test
- **Status:** `pending`
- **Fase:** 4
- **Novo arquivo:** `forzion.tech.Tests/Domain/Properties/MoneyProperties.cs`
- **Commit:** —
- **Data fechado:** —
- **Notas:** CsCheck pra (amount, tax%, rounding). Invariants: banker's rounding + sum preservation.

### F17 — FluentValidation rules não testados direto
- **Status:** `pending`
- **Fase:** 3
- **Novos arquivos:** `forzion.tech.Tests/Validators/**/*ValidatorTests.cs` (uma por validator)
- **Commit:** —
- **Data fechado:** —
- **Notas:** Só 2 validator test classes hoje. Pattern: pra cada rule, teste boundary.

### F18 — a11y `color-contrast` rule disabled
- **Status:** `pending`
- **Fase:** 5
- **Arquivo:** `frontend/e2e/specs/a11y/all-pages-axe.spec.ts` (ou util `runAxe()`)
- **Commit:** —
- **Data fechado:** —
- **Notas:** Disabled por MUI chips/secondary text. Auditar tema; re-ativar; ou tracker explícito de débito.

### F19 — Admin treinador write actions sem E2E
- **Status:** `pending`
- **Fase:** 2
- **Arquivo:** `frontend/e2e/specs/critical/admin-treinador-crud.spec.ts`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Opta-out de aprovar/reprovar/inativar. Seeded fixture user + admin path + rollback via API hook.

### F20 — Visual snapshots tolerantes + sem baseline platform-specific
- **Status:** `pending`
- **Fase:** 5
- **Arquivo:** `frontend/playwright.config.ts:33-34`
- **Commit:** —
- **Data fechado:** —
- **Notas:** `maxDiffPixelRatio: 0.01, threshold: 0.2`. Baseline Linux CI vs macOS local diverge. Documentar protocolo de update.

### F21 — Lighthouse workflow_dispatch sem schedule, sem budgets confirmados
- **Status:** `pending`
- **Fase:** 5
- **Arquivos:** `.github/workflows/lighthouse.yml`, novo `frontend/.lighthouserc.json`
- **Commit:** —
- **Data fechado:** —
- **Notas:** LCP < 2.5s, CLS < 0.1, FID < 100ms. Schedule semanal.

### F22 — ZAP DAST passivo apenas
- **Status:** `pending`
- **Fase:** 5
- **Arquivo:** `.github/workflows/zap.yml`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Sem active rules (SQLi/XSS/path traversal). Upgrade pra Automation Framework.

### F23 — Email verify replay (mesmo token 2x) não testado
- **Status:** `pending`
- **Fase:** 2 (junto com F15)
- **Arquivo:** `forzion.tech.Tests/Application/VerificarEmailHandlerTests.cs`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Token already-used coberto via flag, mas não 2 chamadas com mesmo raw token.

### F24 — Storybook stateful components missing
- **Status:** `pending`
- **Fase:** 5
- **Novos arquivos:** `*.stories.tsx` pra ResponsiveTable, ConfirmDialog, PagamentoCartao, TreinoForm
- **Commit:** —
- **Data fechado:** —
- **Notas:** Loading/error/empty/populated por componente. Limitar a stateful com >3 estados.

### F25 — Rate limit memory eviction sem teste
- **Status:** `pending`
- **Fase:** 4
- **Arquivo:** `frontend/src/lib/rateLimit.test.ts`
- **Commit:** —
- **Data fechado:** —
- **Notas:** MAX_ENTRIES = 10k. Loop > MAX_ENTRIES, assertion oldest evicted.

### F26 — PagamentoPix unmount mid-poll não testado
- **Status:** `pending`
- **Fase:** 4
- **Arquivo:** `frontend/src/app/(aluno)/__tests__/pagamento.test.tsx`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Render → fetch starts → unmount → assert no dangling interval.

### F27 — Coverage thresholds não enforced em CI
- **Status:** `pending`
- **Fase:** 3
- **Arquivos:** `frontend/vitest.config.mts`, CI workflow
- **Commit:** —
- **Data fechado:** —
- **Notas:** Confirmar `--check-coverage` no CI. Ratchet onde possível.

### F28 — Role revoked mid-request E2E
- **Status:** `pending`
- **Fase:** 4
- **Novo arquivo:** `frontend/e2e/specs/security/role-revoked-mid-request.spec.ts`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Treinador inativado enquanto request mid-flight → 403.

### F29 — Stripe Connect onboarding E2E ausente
- **Status:** `pending`
- **Fase:** 4
- **Novo arquivo:** `frontend/e2e/specs/critical/treinador-onboarding-stripe.spec.ts`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Stripe Express onboarding flow.

### F30 — Cross-aggregate App-level integration ausente
- **Status:** `pending`
- **Fase:** 4
- **Novo arquivo:** `forzion.tech.Tests/Application/Integration/TreinadorApprovalCrossAggregateTests.cs`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Approve trainer → Plano loaded + charges_enabled check. Bridge entre unit handler tests e E2E.

---

## Minor (F31-F38)

### F31 — `DateTime.UtcNow` em factories de testes
- **Status:** `pending`
- **Fase:** 5
- **Arquivos:** `forzion.tech.Tests/Domain/Entities/AlunoTests.cs:17`, padrão pelo suite
- **Commit:** —
- **Data fechado:** —
- **Notas:** Padronizar `TestData.Agora`. Não bloqueia (handlers usam TimeProvider), só consistência.

### F32 — Builders sem opt-out de validation
- **Status:** `pending`
- **Fase:** 5
- **Arquivos:** `forzion.tech.Tests/Builders/AlunoBuilder.cs` + outros
- **Commit:** —
- **Data fechado:** —
- **Notas:** Add `.WithValidation(bool)` pra negative tests de handler.

### F33 — DTO snapshot tests vazios
- **Status:** `pending`
- **Fase:** 5
- **Arquivo:** `forzion.tech.Tests/Api/Snapshots/ResponseDtoSnapshots.cs`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Verify.Xunit já configurado. Adicionar snapshot por response DTO; quebra mudança breaking.

### F34 — Pact broker single-point-of-failure (Hostinger)
- **Status:** `deferred`
- **Fase:** Backlog (infra)
- **Arquivo:** Runbook a criar
- **Commit:** —
- **Data fechado:** —
- **Notas:** Out of test-suite scope. Pertence ao backlog infra.

### F35 — Memory leak test só Chromium
- **Status:** `deferred`
- **Fase:** Backlog
- **Arquivo:** `frontend/e2e/specs/critical/navigation-leak.spec.ts`
- **Commit:** —
- **Data fechado:** —
- **Notas:** `performance.memory` é Chromium-only por design da API. Documentado em runbook.

### F36 — Frontend factories inconsistentes
- **Status:** `pending`
- **Fase:** 5
- **Arquivos:** `frontend/src/components/pagamento/PagamentoCartao.test.tsx`, `frontend/src/app/(aluno)/__tests__/pagamento.test.tsx`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Consolidar via `buildPagamento(overrides)` no `frontend/src/test/factories/`.

### F37 — `advanceTime()` export unused
- **Status:** `pending`
- **Fase:** 5
- **Arquivo:** `frontend/src/test/determinism/index.ts:48`
- **Commit:** —
- **Data fechado:** —
- **Notas:** Drop OU documentar quando usar.

### F38 — MSW webhook handlers default 401 silencioso
- **Status:** `done`
- **Fase:** 1
- **Arquivo:** `frontend/src/test/msw/handlers/pagamento.ts`
- **Commit:** `c6e8def`
- **Data fechado:** 2026-05-28
- **Notas:** Defaults dos `/webhooks/stripe` e `/webhooks/resend` removidos — webhooks são server-to-server (Stripe/Resend → backend) e não devem ser hitados por frontend tests. `onUnhandledRequest: "error"` agora falha loud em qualquer chamada acidental. Comment explica a omissão.
