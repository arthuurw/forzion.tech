# E2E (Playwright)

Fase 9 do harness — infra Playwright base. Specs reais entram em Fase 10+.

## Quick start

```bash
# Instala binarios dos browsers (so na primeira vez)
npm run e2e:install

# Roda toda suite contra http://localhost:3000 (suba o app antes: npm run dev)
npm run e2e

# UI interativa
npm run e2e:ui

# So smoke
npm run e2e:smoke
```

## Configuracao por env

| Var | Default | Funcao |
|-----|---------|--------|
| `E2E_BASE_URL` | `http://localhost:3000` | Onde o Playwright aponta |
| `E2E_ADMIN_EMAIL` | _unset_ | Login admin pra storage state |
| `E2E_ADMIN_PASSWORD` | _unset_ | |
| `E2E_ALUNO_EMAIL` | _unset_ | Idem aluno |
| `E2E_ALUNO_PASSWORD` | _unset_ | |
| `E2E_TREINADOR_EMAIL` | _unset_ | Idem treinador |
| `E2E_TREINADOR_PASSWORD` | _unset_ | |
| `CI` | _unset_ | Liga retries=2, workers=4, reporter completo |

Sem `E2E_*_EMAIL/PASSWORD`, o project "setup" pula a geracao do storage state
e tests que dependem de role autenticada sao skip-ados (nao falham).

## Estrutura

```
e2e/
├─ auth.setup.ts            # Project "setup" — gera .auth/*.json por role
├─ .auth/                   # Storage states (gitignored)
│  ├─ admin.json
│  ├─ aluno.json
│  └─ treinador.json
├─ fixtures/
│  ├─ test-base.ts          # extends test com network + consoleErrors
│  ├─ auth.ts               # authStatePath/hasAuthState/useAuthRole
│  └─ network.ts            # slow3G/offline/flakyRoute (CDP)
├─ pages/                   # Page Object Model (Fase 10)
│  └─ BasePage.ts
├─ utils/
│  ├─ stripe.ts             # Cartoes test mode
│  ├─ memory.ts             # measureHeap/forceGC
│  ├─ assert-csp.ts         # parseCsp/assertCspDirective
│  └─ seed.ts               # Stubs — Fase 10 implementa
└─ specs/
   ├─ smoke/                # Fase 10a — 5 smokes (health + login + listar + criar + checkout)
   ├─ critical/             # Fase 10a — 8 specs criticos (auth, CRUD, Stripe, inactivity, etc)
   ├─ security/             # Fase 10b — CSP + cookies + CSRF + rate limit
   ├─ lgpd/                 # Fase 10b — skeletons fail-loud (features ausentes)
   ├─ multi-tab/            # Fase 10b — logout cross-tab + session sync
   ├─ network/              # Fase 10b — slow3G + offline + retry
   ├─ a11y/                 # Fase 10b — @axe-core/playwright varre rotas
   └─ visual/               # Fase 10b — snapshots (baselines Linux only, Fase 17)
```

## Pattern de spec

```ts
import { test, expect, useAuthRole } from "../../fixtures/test-base";

useAuthRole(test, "admin");  // skip se .auth/admin.json nao existir

test("admin lista alunos", async ({ page, network }) => {
  await network.slow3G();
  await page.goto("/admin/alunos");
  await expect(page.getByRole("heading", { name: /alunos/i })).toBeVisible();
});
```

## Sharding

CI usa `--shard N/M` via matrix (Fase 17):

```yaml
strategy:
  matrix:
    shard: [1/4, 2/4, 3/4, 4/4]
    project: [chromium-desktop, firefox-desktop, webkit-desktop]
run: npx playwright test --shard ${{ matrix.shard }} --project ${{ matrix.project }}
```

## Visual snapshots

Visual regression (Fase 11) usa baselines Linux-only via `toHaveScreenshot()`.
**Nao rode `--update-snapshots` localmente** — gera baselines com fontes/OS
diferentes e mascara regressao real.
