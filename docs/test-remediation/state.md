# Test Remediation — State Dashboard

> Atualizado: **2026-05-28** (Fase 3 fechada — 6/7 done + 1 deferred; F12 unblocked após Docker up)
> Branch central: `fix/frontend-dockerfile-npmrc`
> Plano fonte: `~/.claude/plans/fa-a-um-review-completo-virtual-squirrel.md`

## Snapshot global

| Severidade | Total | Pending | In progress | Done | Deferred | Skipped | Blocked |
|------------|-------|---------|-------------|------|----------|---------|---------|
| Critical   | 12    | 4       | 0           | 8    | 0        | 0       | 0       |
| Important  | 18    | 11      | 0           | 7    | 0        | 0       | 0       |
| Minor      | 8     | 5       | 0           | 1    | 2        | 0       | 0       |
| **Total**  | **38**| **20**  | **0**       | **16**|**2**    | **0**   | **0**   |

## Fase ativa

**Fase 4 — Risco médio** (próxima a iniciar)

Fase 3 fechada — 6 done, 1 deferred:
- ✅ F4 Pact provider repo-level (3 handlers convertidos)
- ✅ F5 Pact error contracts (12 consumer; provider-side state handlers diferidos)
- ✅ F8 Stryker scope expansion (backend matrix já existia; frontend +components)
- ✅ F12 Concurrent billing race (Testcontainers + Barrier — confirmado em run real)
- ✅ F17 FluentValidation tests diretos (7/15 core validators; 8 admin diferidos)
- ✅ F27 Coverage threshold enforcement (vitest 4 enforce automático documentado)
- ⏭ F6 MSW migration — DEFERRED Fase 4 (1551 LOC, scope grande)

## Fases concluídas

- ✅ **Fase 1** (2026-05-28) — Stop false confidence: F1, F2, F13, F38 done.
- ✅ **Fase 2** (2026-05-28) — Cobertura crítica: F9, F10, F11, F14, F15, F19, F23 done.
- ✅ **Fase 3** (2026-05-28) — Endurecer gates: F4, F5, F8, F12, F17, F27 done. F6 deferred Fase 4.

## Fases (visão geral)

| Fase | Foco | Estimativa | Progresso | Bloqueia release? |
|------|------|------------|-----------|-------------------|
| 1 | Stop false confidence | 1-2 dias | **4/4** ✅ | Não |
| 2 | Cobertura crítica ausente | 3-5 dias | **7/7** ✅ | **Sim** (resolvido) |
| 3 | Endurecer gates | 2-3 dias | **6/7** ✅ (F6 deferred) | Não |
| 4 | Risco médio | ~1 semana | 0/8 + F6 reaberto | Não |
| 5 | Polish | Backlog contínuo | 0/13 | Não |

## Métricas suite atual (baseline)

Capturado em **2026-05-28** após Fase 3 (commit `90040c0` + commit F17 TBD):

| Métrica | Valor | Fonte |
|---------|-------|-------|
| Backend tests (Category != Integration) | 1160 pass (+45 F17) | `dotnet test forzion.tech.slnx --filter "Category!=Integration"` |
| Frontend tests (vitest, 32 suites) | 380 pass | `npm test` em `frontend/` |
| Frontend Pact consumer (happy + errors) | 4 + 12 = 16 | `npm run test:contract` |
| Backend E2E (Integration) — F12 race | 1 pass (~10s) | `dotnet test --filter ConcurrentBillingRace` (Docker) |
| Backend build warnings | 0 | `dotnet build -c Release` |
| Frontend lint errors | 0 | `npm run lint` |
| Frontend type errors | 0 | `npx tsc --noEmit` |
| MSW handlers populados (admin/aluno/treinador/pagamento) | 4/4 | commit `2303a5d` |
| Pact provider repo-level mocks | 4/4 (todos handlers convertidos) | commits `d7e395e` + `90040c0` |
| Pact consumer error contracts | 12 (4 endpoints × 3 statuses) | commit `90040c0` (output em `pacts-errors/`) |
| Stryker scope (backend) | Domain + Application (matrix) | `.github/workflows/mutation.yml` |
| Stryker scope (frontend) | `src/lib` + `src/hooks` + `src/components` | `frontend/stryker.conf.json` |
| Stryker break threshold (backend) | 40 | `stryker-config.json` |
| Stryker break threshold (frontend) | 60 (era 75 — runway pra components) | `frontend/stryker.conf.json` |
| Coverage thresholds enforced em CI | ✅ vitest 4 per-path automático | `frontend/vitest.config.mts` comment |
| GH Actions pinned em SHA | 0/N | C12 (out-of-scope deste plano) |

## Métricas-alvo

- Fase 1 fim: 0 specs E2E "no-op" passing ✅
- Fase 2 fim: cobertura E2E pra password reset, email verification, treinador signup, token revocation, admin treinador write actions, Resend webhook idempotência ✅
- Fase 3 fim: Pact 4/4 repo-level + error paths 401/404/500 cobertos (consumer-side) + Stryker components incluído + `--coverage` enforcement documentado + concurrent billing race E2E provado ✅
- Fase 4 fim: Stripe checkout sem `skip()`, money rounding property-tested, MSW migration completa (F6 reaberto), 8 validators admin-side restantes (F17 cont.)
- Fase 5: Storybook stories para 4 stateful components + DTO snapshots + a11y color-contrast re-enabled

## Última sessão

**2026-05-28** — Fase 3 implementada e fechada parcialmente. Commits `90040c0` (F4+F5+F8+F27) + commit F17 TBD. Ver `log.md` pra detalhes.

## Riscos / pendências pra reabrir em Fase 4

- **F6** (MSW migration) — 5 arquivos / 1551 LOC. Plano: 1 arquivo por sessão, começar pelo menor (`saude/page.client.test.tsx`, 91 LOC). `admin.test.ts` pode ser deletado APÓS expansão de `admin.msw.test.ts` cobrir ~30 endpoints.
- **F5 provider-side** — Implementar state handlers em `ForzionApiProviderTests.cs` (`WithProviderStateUrl` + `/_pact/provider-states` endpoint + middleware que retorna ProblemDetails baseado em state). Promover `pacts-errors/` → `pacts/` no fim. Estimativa: ~30 min.
- **F17 admin-side** — 8 validators restantes (GruposMusculares Criar+Atualizar, HealthReport-extra, Exercicios/Criar, Pacotes/Atualizar, Planos/Criar+Atualizar, Treinos/AdicionarExercicio). Mesmo pattern de `CoreValidatorsTests.cs`.
