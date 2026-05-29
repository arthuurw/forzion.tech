# Test Remediation — State Dashboard

> Atualizado: **2026-05-28** (TODAS as 5 fases fechadas — 36/38 done + 2 deferred)
> Branch central: `fix/frontend-dockerfile-npmrc`
> Plano fonte: `~/.claude/plans/fa-a-um-review-completo-virtual-squirrel.md`

## Snapshot global

| Severidade | Total | Pending | In progress | Done | Deferred | Skipped | Blocked |
|------------|-------|---------|-------------|------|----------|---------|---------|
| Critical   | 12    | 0       | 0           | 12   | 0        | 0       | 0       |
| Important  | 18    | 0       | 0           | 18   | 0        | 0       | 0       |
| Minor      | 8     | 0       | 0           | 6    | 2        | 0       | 0       |
| **Total**  | **38**| **0**   | **0**       | **36**|**2**    | **0**   | **0**   |

## Fase ativa

**TODAS as fases fechadas.** Backlog futuro fica fora deste tracker.

Fase 5 — Polish (10 tasks fechadas):
- ✅ F18 a11y color-contrast — runAxeStrict + ratchet spec com ceiling explicito
- ✅ F20 Visual snapshots baselines — snapshotPathTemplate platform-specific
- ✅ F21 Lighthouse — schedule semanal (Quarta 06:00 UTC); budgets confirmados
- ✅ F22 ZAP active rules — input mode=full ativa action-full-scan
- ✅ F24 Storybook stories — ConfirmDialog + ResponsiveTable (PagamentoCartao/TreinoForm deferidos)
- ✅ F31 TestData.Agora padronizado — 20 Domain entity tests
- ✅ F32 Builders opt-in validation — AlunoBuilder.BuildUnsafe() pattern
- ✅ F33 DTO snapshots — 14 total (6 preexistentes + 8 novos)
- ✅ F36 Frontend factories consolidadas — buildPagamento via factories
- ✅ F37 advanceTime() unused — dropado

## Fases concluídas

- ✅ **Fase 1** (2026-05-28) — Stop false confidence: F1, F2, F13, F38 done.
- ✅ **Fase 2** (2026-05-28) — Cobertura crítica: F9, F10, F11, F14, F15, F19, F23 done.
- ✅ **Fase 3** (2026-05-28) — Endurecer gates: F4, F5, F6, F8, F12, F17, F27 done.
- ✅ **Fase 4** (2026-05-28) — Risco médio: F3, F5b, F7, F16, F17b, F25, F26, F28, F29, F30 done.
- ✅ **Fase 5** (2026-05-28) — Polish: F18, F20, F21, F22, F24, F31, F32, F33, F36, F37 done.

## Fases (visão geral)

| Fase | Foco | Progresso | Bloqueia release? |
|------|------|-----------|-------------------|
| 1 | Stop false confidence | **4/4** ✅ | Não |
| 2 | Cobertura crítica ausente | **7/7** ✅ | **Sim** (resolvido) |
| 3 | Endurecer gates | **7/7** ✅ | Não |
| 4 | Risco médio | **10/10** ✅ | Não |
| 5 | Polish | **10/10** ✅ | Não |
| **Total** | | **38/38 endereçados (36 done + 2 deferred)** | |

## Deferred (out of test-suite scope)

- **F34** Pact broker SPOF (Hostinger) — infra runbook, não test scope.
- **F35** Memory leak test Chromium-only — limitacao plataforma (`performance.memory` API).

## Métricas suite atual (final)

Capturado em **2026-05-28** após Fase 5:

| Métrica | Valor | Fonte |
|---------|-------|-------|
| Backend tests (Category != Integration) | ~1255 pass | `dotnet test forzion.tech.slnx --filter "Category!=Integration"` |
| Frontend tests (vitest, 31 suites) | 341 pass | `npm test` |
| Frontend Pact consumer (happy + errors) | 16 em `pacts/` | `npm run test:contract` |
| Backend E2E (Integration) | 1+ pass | `dotnet test --filter ConcurrentBillingRace` (Docker) |
| DTO snapshots | 14 (todos response DTOs principais) | `forzion.tech.Tests/Api/Snapshots/Snapshots/` |
| Validator tests diretos | 87 (45 core + 42 admin) | `Application/Validators/*` |
| Money calculations property-tested | 8 invariantes (CsCheck) | `MoneyCentavosProperties.cs` |
| Pact provider state handlers | ✅ (F5b) | `ForzionApiProviderTests.cs` |
| Storybook stories | 4 ui + 4 stateful = 8 total | `src/components/ui/*.stories.tsx` |
| a11y color-contrast tracker | 2 paginas com ceiling explicito | `color-contrast-ratchet.spec.ts` |
| Lighthouse schedule | Semanal (Quarta) | `.github/workflows/lighthouse.yml` |
| ZAP active rules | Opt-in via `mode=full` | `.github/workflows/zap.yml` |
| Visual snapshots platform-specific | ✅ | `playwright.config.ts` snapshotPathTemplate |
| Backend build warnings | 0 | `dotnet build -c Release` |
| Frontend lint errors | 0 | `npm run lint` |
| Frontend type errors | 0 | `npx tsc --noEmit` |

## Backlog futuro (sem deadline)

- **Stripe webhook stripe-cli stub** — completar F3/F29 fluxo end-to-end real.
- **F28 "true mid-request" variant** — backend hook `E2E_REQUEST_DELAY_MS`.
- **F24 PagamentoCartao + TreinoForm stories** — quando Storybook ganhar MSW addon completo.
- **F32 BuildUnsafe pattern em outros builders** — TreinadorBuilder/AssinaturaBuilder sob demanda.
- **F18 ratchet pra 0** — quando MUI tema for ajustado, baixar CEILING_LOGIN/CEILING_CADASTRO ate 0 e mover color-contrast pra runAxe default.

## Última sessão

**2026-05-28** — Fase 5 implementada e fechada em 1 batch. Ver `log.md`.
