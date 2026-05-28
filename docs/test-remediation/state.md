# Test Remediation — State Dashboard

> Atualizado: **2026-05-28** (Fase 1 concluída)
> Branch central: `fix/frontend-dockerfile-npmrc`
> Plano fonte: `~/.claude/plans/fa-a-um-review-completo-virtual-squirrel.md`

## Snapshot global

| Severidade | Total | Pending | In progress | Done | Deferred | Skipped | Blocked |
|------------|-------|---------|-------------|------|----------|---------|---------|
| Critical   | 12    | 10      | 0           | 2    | 0        | 0       | 0       |
| Important  | 18    | 17      | 0           | 1    | 0        | 0       | 0       |
| Minor      | 8     | 5       | 0           | 1    | 2        | 0       | 0       |
| **Total**  | **38**| **32**  | **0**       | **4**| **2**    | **0**   | **0**   |

## Fase ativa

**Fase 2 — Cobertura crítica ausente** (3-5 dias estimados, BLOQUEIA release)

Tasks pendentes:
- [ ] F10 Password reset E2E + handler tests
- [ ] F9 Token revocation E2E
- [ ] F11 Resend webhook idempotência
- [ ] F14 Treinador signup E2E
- [ ] F15 + F23 Email verification E2E + replay test
- [ ] F19 Admin treinador write actions E2E

Critério pra fechar fase 2: 6 above = `done` em `findings.md`.

## Fases concluídas

- ✅ **Fase 1** (2026-05-28) — Stop false confidence: F1, F2, F13, F38 done.

## Fases (visão geral)

| Fase | Foco | Estimativa | Progresso | Bloqueia release? |
|------|------|------------|-----------|-------------------|
| 1 | Stop false confidence | 1-2 dias | **4/4** ✅ | Não |
| 2 | Cobertura crítica ausente | 3-5 dias | 0/6 | **Sim** |
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

**2026-05-28** — Fase 1 implementada e fechada (F38, F13, F2, F1 → done). 4 commits em `fix/frontend-dockerfile-npmrc`. Ver `log.md` pra detalhes.
