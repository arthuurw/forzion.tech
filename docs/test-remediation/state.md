# Test Remediation — State Dashboard

> Atualizado: **2026-05-28** (Fase 4 COMPLETA — 9/9 done incluindo carryovers)
> Branch central: `fix/frontend-dockerfile-npmrc`
> Plano fonte: `~/.claude/plans/fa-a-um-review-completo-virtual-squirrel.md`

## Snapshot global

| Severidade | Total | Pending | In progress | Done | Deferred | Skipped | Blocked |
|------------|-------|---------|-------------|------|----------|---------|---------|
| Critical   | 12    | 0       | 0           | 12   | 0        | 0       | 0       |
| Important  | 18    | 4       | 0           | 14   | 0        | 0       | 0       |
| Minor      | 8     | 5       | 0           | 1    | 2        | 0       | 0       |
| **Total**  | **38**| **9**   | **0**       | **27**|**2**    | **0**   | **0**   |

## Fase ativa

**Fase 5 — Polish** (próxima)

Fase 4 100% fechada (9 tasks incluindo F5b/F17b carryovers da Fase 3):
- ✅ F3 Stripe checkout seeded — garantirPagamentoPendente() via treinador API
- ✅ F5b Pact provider state handlers — middleware + WithProviderStateUrl
- ✅ F7 Stripe partial mock (useStripe/useElements realistas)
- ✅ F16 Money rounding property tests (CsCheck, 8 invariants)
- ✅ F17b Admin-side validators (7 validators, 42 tests)
- ✅ F25 Rate limit MAX_ENTRIES eviction (3 tests)
- ✅ F26 PagamentoPix unmount mid-poll (2 tests)
- ✅ F28 Role revoked mid-request E2E (sequential + fixme variante)
- ✅ F29 Stripe Connect onboarding E2E
- ✅ F30 Cross-aggregate App-level integration (3 tests)

## Fases concluídas

- ✅ **Fase 1** (2026-05-28) — Stop false confidence: F1, F2, F13, F38 done.
- ✅ **Fase 2** (2026-05-28) — Cobertura crítica: F9, F10, F11, F14, F15, F19, F23 done.
- ✅ **Fase 3** (2026-05-28) — Endurecer gates: F4, F5, F6, F8, F12, F17, F27 done.
- ✅ **Fase 4** (2026-05-28) — Risco médio: F3, F5b, F7, F16, F17b, F25, F26, F28, F29, F30 done.

## Fases (visão geral)

| Fase | Foco | Estimativa | Progresso | Bloqueia release? |
|------|------|------------|-----------|-------------------|
| 1 | Stop false confidence | 1-2 dias | **4/4** ✅ | Não |
| 2 | Cobertura crítica ausente | 3-5 dias | **7/7** ✅ | **Sim** (resolvido) |
| 3 | Endurecer gates | 2-3 dias | **7/7** ✅ | Não |
| 4 | Risco médio | ~1 semana | **10/10** ✅ (8 originais + F5b + F17b carryovers) | Não |
| 5 | Polish | Backlog contínuo | 0/9 | Não |

## Métricas suite atual (baseline)

Capturado em **2026-05-28** após Fase 4:

| Métrica | Valor | Fonte |
|---------|-------|-------|
| Backend tests (Category != Integration) | ~1245 pass | `dotnet test forzion.tech.slnx --filter "Category!=Integration"` |
| Frontend tests (vitest, 31 suites pós-F6) | 341 pass (+9 desde F6) | `npm test` em `frontend/` |
| Frontend Pact consumer (happy + errors) | 16 pass em `pacts/` (promovido de pacts-errors) | `npm run test:contract` |
| Backend E2E (Integration) — F12 race | 1 pass (~10s) | `dotnet test --filter ConcurrentBillingRace` (Docker) |
| Backend build warnings | 0 | `dotnet build -c Release` |
| Frontend lint errors | 0 | `npm run lint` |
| Frontend type errors | 0 | `npx tsc --noEmit` |
| MSW handlers populados (admin/aluno/treinador/pagamento) | 4/4 | commit `2303a5d` |
| Pact provider repo-level mocks | 4/4 (todos handlers convertidos) | commits `d7e395e` + `90040c0` |
| Pact provider state handlers | ✅ implementado (F5b) | `ForzionApiProviderTests.cs` |
| Stryker scope (backend) | Domain + Application (matrix) | `.github/workflows/mutation.yml` |
| Stryker scope (frontend) | `src/lib` + `src/hooks` + `src/components` | `frontend/stryker.conf.json` |
| Validator tests diretos | 87 (45 core + 42 admin) | `forzion.tech.Tests/Application/Validators/*` |
| Money calculations property-tested | 8 invariantes (CsCheck) | `MoneyCentavosProperties.cs` |
| Coverage thresholds enforced em CI | ✅ vitest 4 per-path automático | `frontend/vitest.config.mts` comment |

## Métricas-alvo

- Fase 1 fim: 0 specs E2E "no-op" passing ✅
- Fase 2 fim: cobertura E2E pra password reset, email verification, treinador signup, token revocation, admin treinador write actions, Resend webhook idempotência ✅
- Fase 3 fim: Pact 4/4 repo-level + error paths cobertos + Stryker components + concurrent billing race E2E ✅
- Fase 4 fim: Stripe checkout sem skip, money property-tested, MSW migration completa, Pact provider state handlers, all validators, cross-aggregate integration ✅
- Fase 5: Storybook stories para 4 stateful components + DTO snapshots + a11y color-contrast re-enabled + Lighthouse budgets + ZAP active rules + factories padronizadas

## Última sessão

**2026-05-28** — Fase 4 implementada e fechada em 1 batch. Ver `log.md`.

## Riscos / pendências pra Fase 5

- **F18 a11y color-contrast** — auditar MUI tema; re-ativar rule.
- **F20 Visual snapshots baselines** — protocolo Linux CI vs macOS local.
- **F21 Lighthouse budgets** — schedule + `.lighthouserc.json`.
- **F22 ZAP active rules** — Automation Framework com SQLi/XSS.
- **F24 Storybook stories** — ResponsiveTable, ConfirmDialog, PagamentoCartao, TreinoForm.
- **F31-F37** — padronização (TestData.Agora, builders, frontend factories, drop advanceTime, DTO snapshots).
- **Stripe webhook stripe-cli stub** — completar F3/F29 fluxo end-to-end.
- **F28 true mid-request variant** — backend hook E2E_REQUEST_DELAY_MS.
