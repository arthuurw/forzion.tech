# Test Remediation — State Dashboard

> Atualizado: **2026-05-28** (Fase 2 concluída)
> Branch central: `fix/frontend-dockerfile-npmrc`
> Plano fonte: `~/.claude/plans/fa-a-um-review-completo-virtual-squirrel.md`

## Snapshot global

| Severidade | Total | Pending | In progress | Done | Deferred | Skipped | Blocked |
|------------|-------|---------|-------------|------|----------|---------|---------|
| Critical   | 12    | 7       | 0           | 5    | 0        | 0       | 0       |
| Important  | 18    | 13      | 0           | 5    | 0        | 0       | 0       |
| Minor      | 8     | 5       | 0           | 1    | 2        | 0       | 0       |
| **Total**  | **38**| **25**  | **0**       | **11**|**2**    | **0**   | **0**   |

## Fase ativa

**Fase 3 — Endurecer gates** (2-3 dias estimados, parcialmente bloqueia release)

Tasks pendentes:
- [ ] F12 Concurrent billing race (Testcontainers E2E)
- [ ] F4 Pact provider repo-level mocking (3 handlers restantes)
- [ ] F5 Pact error contracts (401/404/500)
- [ ] F8 Stryker scope expansion (Application + components)
- [ ] F6 MSW migration completa (5-6 arquivos)
- [ ] F27 Coverage threshold enforcement em CI
- [ ] F17 FluentValidation tests diretos

Critério pra fechar fase 3: 7 above = `done` em `findings.md`.

## Fases concluídas

- ✅ **Fase 1** (2026-05-28) — Stop false confidence: F1, F2, F13, F38 done.
- ✅ **Fase 2** (2026-05-28) — Cobertura crítica: F9, F10, F11, F14, F15, F19, F23 done.

## Fases (visão geral)

| Fase | Foco | Estimativa | Progresso | Bloqueia release? |
|------|------|------------|-----------|-------------------|
| 1 | Stop false confidence | 1-2 dias | **4/4** ✅ | Não |
| 2 | Cobertura crítica ausente | 3-5 dias | **7/7** ✅ | **Sim** (resolvido) |
| 3 | Endurecer gates | 2-3 dias | 0/7 | Parcial (F12, F4 prioritários se billing tocado) |
| 4 | Risco médio | ~1 semana | 0/8 | Não |
| 5 | Polish | Backlog contínuo | 0/13 | Não |

## Métricas suite atual (baseline)

Capturado em **2026-05-28** após sessão anterior (14 commits merged em `fix/frontend-dockerfile-npmrc`):

| Métrica | Valor | Fonte |
|---------|-------|-------|
| Backend tests (Category != Integration) | 1115 pass | `dotnet test forzion.tech.slnx --filter "Category!=Integration"` |
| Frontend tests (vitest, 32 suites) | 380 pass | `npm test` em `frontend/` |
| Backend build warnings | 0 | `dotnet build -c Release` |
| Frontend lint errors | 0 | `npm run lint` |
| Frontend lint warnings | 53 (pre-existentes) | Idem |
| Frontend type errors | 0 | `npx tsc --noEmit` |
| MSW handlers populados (admin/aluno/treinador/pagamento) | 4/4 | commit `2303a5d` |
| Pact provider repo-level mocks | 1/4 (ListarAlunosAdmin) | commit `d7e395e` |
| Stryker scope (backend) | Domain only | `stryker-config.json` |
| Stryker scope (frontend) | `src/lib` + `src/hooks` | `frontend/stryker.conf.json` |
| Stryker break threshold (backend) | 40 | `stryker-config.json` |
| Coverage thresholds enforced em CI | ⚠️ não confirmado | F27 pendente |
| GH Actions pinned em SHA | 0/N | C12 (out-of-scope deste plano) |

## Métricas-alvo

- Fase 1 fim: 0 specs E2E "no-op" passing
- Fase 2 fim: cobertura E2E pra password reset, email verification, treinador signup, token revocation, admin treinador write actions, Resend webhook idempotência
- Fase 3 fim: Pact 4/4 repo-level + error paths 401/404/500 cobertos + Stryker Application incluído + `--check-coverage` em CI
- Fase 4 fim: Stripe checkout sem `skip()`, race condition concurrency provada, money rounding property-tested
- Fase 5: Storybook stories para 4 stateful components + DTO snapshots + a11y color-contrast re-enabled

## Última sessão

**2026-05-28** — Fase 2 implementada e fechada (F9, F10, F11, F14, F15, F19, F23 → done). 9 commits em `fix/frontend-dockerfile-npmrc` (incluindo 2 commits de fix de lint mangling em F19). Ver `log.md` pra detalhes.
