# Frontend Test Harness — Plano de Implementação

Stack para `frontend/` (Next 16 + React 19 + MUI v9 + Stripe).
Cobre: unit, integration, contract, E2E, visual, a11y, perf, security, mutation, property-based, observability.

Última revisão: 2026-05-22.

---

## 1. Estado base

- Vitest 4 + happy-dom + jsdom (duplicado) + `@testing-library/*`
- Coverage v8 global: 85L / 75B / 74F / 83S
- 282 testes / 97 suites, todos verdes
- `src/test/` flat (17 arquivos)
- CI: `tsc --noEmit` + `npm run test:coverage`
- Mock API: `vi.mock("axios")` ad-hoc
- `src/app/api/**` excluído de coverage — **gap crítico**

## 2. Decisões fixadas

| # | Item | Escolha |
|---|------|---------|
| 1 | DOM env | jsdom único |
| 2 | API mock | MSW v2, handlers tipados via OpenAPI |
| 3 | E2E target | PR preview deploy + smoke pós-deploy homolog |
| 4 | Stripe | test mode real (checkout) + MSW (APIs internas) |
| 5 | Coverage per-path | lib 95/90 · hooks 90/85 · components 85/75 · app/api 90/85 · app 70/60 |
| 6 | Vitest layout | `projects: [unit, integration, api]` |
| 7 | Mutation score | ≥ 75% em `src/lib/**` e `src/hooks/**` |
| 8 | Determinismo | tempo, random, UUID, motion sempre controlados |
| 9 | Sentry | day 1 |
| 10 | Deps | Renovate, não Dependabot |

## 3. Stack por camada

| Camada | Ferramenta |
|--------|-----------|
| Unit | Vitest 4 (project `unit`, env `node`) |
| Integration | Vitest 4 (project `integration`, env `jsdom`, MSW) |
| API routes | Vitest (project `api`, env `node`, NextRequest helpers) |
| Property-based | `fast-check` + `@fast-check/vitest` |
| Mutation | Stryker (`@stryker-mutator/vitest-runner`) |
| Contract | Pact (consumer-driven) + broker |
| Schema drift | `openapi-typescript` + `openapi-msw` |
| Component catalog | Storybook 8 + test-runner + MSW addon |
| E2E | Playwright (3 browsers × 2 viewports, sharded) + POM |
| Visual | Playwright `toHaveScreenshot()` (baselines Linux only) |
| A11y | `vitest-axe` (componentes) + `@axe-core/playwright` (E2E) |
| Perf synthetic | Lighthouse CI + bundle-analyzer |
| Perf RUM | `web-vitals` → Sentry |
| SAST | ESLint security + CodeQL + gitleaks |
| DAST | OWASP ZAP contra preview deploy |
| Supply chain | npm audit + osv-scanner + CycloneDX SBOM + license-checker |
| Quality gates | ESLint strict + commitlint + husky + lint-staged |
| Error tracking | Sentry (errors + replay + perf + source maps) |
| Test analytics | Codecov flags + Datadog Test Visibility / BuildKite |

## 4. Estrutura

```
frontend/
├─ src/
│  ├─ <feature>/
│  │  ├─ Foo.tsx
│  │  ├─ Foo.test.tsx
│  │  ├─ Foo.stories.tsx
│  │  ├─ Foo.a11y.test.tsx
│  │  └─ Foo.property.test.ts
│  ├─ app/api/<route>/
│  │  ├─ route.ts
│  │  └─ route.test.ts
│  └─ test/
│     ├─ setup/
│     │  ├─ unit.ts
│     │  ├─ integration.ts
│     │  ├─ api.ts
│     │  └─ mocks/
│     │     ├─ matchMedia.ts
│     │     ├─ intersectionObserver.ts
│     │     ├─ resizeObserver.ts
│     │     ├─ next-navigation.ts
│     │     ├─ next-image.ts
│     │     ├─ next-headers.ts
│     │     └─ stripe.ts
│     ├─ determinism/
│     │  ├─ time.ts
│     │  ├─ random.ts
│     │  ├─ uuid.ts
│     │  └─ motion.ts
│     ├─ render.tsx
│     ├─ factories/
│     │  ├─ aluno.ts
│     │  ├─ treinador.ts
│     │  ├─ plano.ts
│     │  └─ pagamento.ts
│     ├─ msw/
│     │  ├─ server.ts
│     │  ├─ browser.ts
│     │  ├─ db.ts
│     │  ├─ types.ts                # gerado de OpenAPI
│     │  └─ handlers/{admin,aluno,treinador,pagamento,auth,index}.ts
│     ├─ pact/
│     │  ├─ consumer.test.ts
│     │  └─ pacts/
│     └─ fixtures/
├─ e2e/
│  ├─ playwright.config.ts
│  ├─ global-setup.ts
│  ├─ global-teardown.ts
│  ├─ fixtures/{auth,network,test-base}.ts
│  ├─ pages/                        # POM
│  │  └─ {Login,AdminAluno,AdminTreinador,Checkout,Lgpd,AlunoTreino}Page.ts
│  ├─ specs/
│  │  ├─ smoke/
│  │  ├─ critical/
│  │  ├─ security/                  # CSP, cookies, CSRF, rate-limit
│  │  ├─ lgpd/                      # consent, delete, export
│  │  ├─ multi-tab/
│  │  ├─ network/                   # slow3G, offline, retry
│  │  ├─ a11y/
│  │  └─ visual/
│  └─ utils/{seed,stripe,memory,assert-csp}.ts
├─ .storybook/{main,preview,test-runner}.ts
├─ stryker.conf.json
├─ pact-broker.config.ts
├─ zap.yaml
├─ renovate.json
├─ lighthouserc.json
├─ vitest.config.mts
├─ playwright.config.ts
├─ commitlint.config.ts
├─ .lintstagedrc.json
├─ tsconfig.test.json
└─ .husky/{pre-commit,commit-msg}

.github/
├─ CODEOWNERS
├─ pull_request_template.md
└─ workflows/
   ├─ ci.yml                        # PR gate
   ├─ codeql.yml
   ├─ zap.yml
   ├─ mutation.yml                  # semanal + manual
   ├─ contract.yml
   ├─ openapi-drift.yml
   ├─ smoke.yml                     # pós-deploy
   ├─ sbom.yml
   └─ release.yml
```

## 5. Configurações-chave

### vitest.config.mts

```ts
projects: [
  { name: "unit",        environment: "node",  pool: "threads",
    include: ["src/lib/**/*.test.ts", "src/hooks/**/*.test.ts"] },
  { name: "integration", environment: "jsdom", pool: "forks",
    include: ["src/components/**/*.test.tsx", "src/app/**/*.client.test.tsx"],
    setupFiles: ["./src/test/setup/integration.ts"] },
  { name: "api",         environment: "node",
    include: ["src/app/api/**/*.test.ts"],
    setupFiles: ["./src/test/setup/api.ts"] },
],
coverage: {
  provider: "v8",
  reporter: ["text", "json", "html", "lcov", "text-summary"],
  exclude: ["node_modules/", "src/test/**", "src/types/**",
            "**/*.d.ts", "**/*.config.*", "**/*.stories.tsx",
            "**/*.property.test.ts", "e2e/**"],
  thresholds: {
    "src/lib/**":        { lines: 95, branches: 90, functions: 95, statements: 95 },
    "src/hooks/**":      { lines: 90, branches: 85, functions: 90, statements: 90 },
    "src/components/**": { lines: 85, branches: 75, functions: 85, statements: 85 },
    "src/app/api/**":    { lines: 90, branches: 85, functions: 90, statements: 90 },
    "src/app/**":        { lines: 70, branches: 60, functions: 70, statements: 70 },
  },
}
```

### Setup determinismo

`src/test/setup/unit.ts`:
```ts
import "@testing-library/jest-dom";
import { installDeterminism } from "../determinism";
import "./mocks";

beforeEach(() => {
  vi.clearAllMocks();
  installDeterminism({ time: "2026-01-01T12:00:00.000Z", seed: 42 });
});
afterEach(() => vi.useRealTimers());
```

`determinism/`:
- `time.ts`: `vi.useFakeTimers({ now })` + `dayjs.locale("pt-br")`
- `random.ts`: `Math.random` ← seedrandom
- `uuid.ts`: stub `crypto.randomUUID` com contador
- `motion.ts`: força `prefers-reduced-motion: reduce`

Mocks globais: matchMedia, IntersectionObserver, ResizeObserver, next/navigation, next/image, next/headers, Stripe.js.

### playwright.config.ts

```ts
export default defineConfig({
  testDir: "./e2e/specs",
  fullyParallel: true,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 4 : undefined,
  reporter: [["html"], ["junit", { outputFile: "playwright-report/junit.xml" }],
             ["github"], ["blob"]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:3000",
    trace: "on-first-retry",
    video: "retain-on-failure",
    screenshot: "only-on-failure",
    locale: "pt-BR",
    timezoneId: "America/Sao_Paulo",
  },
  projects: [
    { name: "setup", testMatch: /global-setup/ },
    { name: "chromium-desktop", use: devices["Desktop Chrome"], dependencies: ["setup"] },
    { name: "firefox-desktop",  use: devices["Desktop Firefox"], dependencies: ["setup"] },
    { name: "webkit-desktop",   use: devices["Desktop Safari"], dependencies: ["setup"] },
    { name: "mobile-chrome",    use: devices["Pixel 5"], dependencies: ["setup"] },
    { name: "mobile-safari",    use: devices["iPhone 13"], dependencies: ["setup"] },
  ],
  expect: { toHaveScreenshot: { maxDiffPixelRatio: 0.01, threshold: 0.2 } },
});
```

CI shard:
```yaml
strategy:
  matrix:
    shard: [1/4, 2/4, 3/4, 4/4]
    project: [chromium-desktop, firefox-desktop, webkit-desktop]
```

### stryker.conf.json

```json
{
  "testRunner": "vitest",
  "checkers": ["typescript"],
  "tsconfigFile": "tsconfig.json",
  "mutate": ["src/lib/**/*.ts", "src/hooks/**/*.ts",
             "!src/**/*.test.*", "!src/**/*.stories.*"],
  "thresholds": { "high": 85, "low": 75, "break": 75 },
  "reporters": ["html", "clear-text", "progress", "dashboard"]
}
```

### renovate.json

```json
{
  "extends": ["config:base", "schedule:weeknights", ":semanticCommits"],
  "packageRules": [
    { "matchUpdateTypes": ["patch"], "automerge": true },
    { "matchPackagePatterns": ["^@playwright"], "groupName": "playwright" },
    { "matchPackagePatterns": ["^@storybook"], "groupName": "storybook" },
    { "matchPackagePatterns": ["^@vitest"], "groupName": "vitest" }
  ],
  "lockFileMaintenance": { "enabled": true, "schedule": ["before 5am on monday"] }
}
```

### lighthouserc.json budgets

```json
{
  "ci": {
    "assert": {
      "preset": "lighthouse:recommended",
      "assertions": {
        "categories:performance":   ["error", { "minScore": 0.85 }],
        "categories:accessibility": ["error", { "minScore": 0.95 }],
        "categories:best-practices":["error", { "minScore": 0.9 }],
        "largest-contentful-paint": ["error", { "maxNumericValue": 2500 }],
        "cumulative-layout-shift":  ["error", { "maxNumericValue": 0.1 }],
        "total-blocking-time":      ["error", { "maxNumericValue": 300 }]
      }
    }
  }
}
```

## 5.1 Workflow de desenvolvimento

**Regras invariáveis** (sem exceção):

| Item | Padrão |
|------|--------|
| Branch base | `homolog` (apenas centralizador, sem dev direto) |
| Branch por fase | `chore/harness-fase<N>-<slug-kebab>` |
| Commits | Conventional Commits (`chore(frontend):`, `test(frontend):`, `feat(frontend):`) |
| Commits parciais | permitidos dentro da branch (squash no merge) |
| PR título | `[Harness Fase <N>] <título>` |
| PR body | link plano + checklist do escopo + resultado validação |
| Merge | squash → 1 commit por fase em `homolog` |
| Branch após merge | deletado |

**Validação obrigatória (pre-commit hook + CI)**:

```
npm test               # vitest run, todos verdes
npx tsc --noEmit       # zero erros tipo
npm run lint           # eslint, zero erros
```

Pre-commit local via husky bloqueia commit se qualquer um falhar.
CI no GH Actions repete validação no push, bloqueia merge se falhar.
Nenhum bypass: `--no-verify` proibido.

**Status fases concluídas**:

| Fase | Commit | Branch usado |
|------|--------|--------------|
| 0 — Limpeza jsdom | `6a3a636` (no squash `bd086ce`) | direto em `homolog` (legado, antes da regra) |
| 1 — Setup + determinismo | `bd086ce` (PR #11) | `chore/harness-fase1-setup-determinismo` |
| Docs rationale inicial | `8436426` (PR #12) | `docs/harness-rationale` |
| 2 — Vitest projects + coverage per-path | `1774421` (PR #13) | `chore/harness-fase2-vitest-projects` |
| Infra — expor Swagger em homologacao | `7ab3cba` (PR #14) | `feat/infra-expose-swagger-homolog` |
| Fix CI — restart nginx em deploy | `8c72760` (PR #15) | `fix/ci-restart-nginx-on-deploy` |
| Fix backend — AllowedHosts homologacao | `ab7cee6` (PR #16) | `fix/backend-allowed-hosts-homologacao` |
| 3 — MSW + OpenAPI codegen + factories | `af32fab` (PR #17) | `chore/harness-fase3-msw-openapi` |
| 4 — Property-based testing | `898473b` (PR #18) | `chore/harness-fase4-property-based` |
| 5a — Co-localização de testes | `efda572` (PR #19) | `chore/harness-fase5-migracao-msw` |
| 5b — Migração auth/fetch para MSW | `5577436` (PR #20) | `chore/harness-fase5b-msw-migration` |
| 6 — API routes testing completo | `1947dee` (PR #21) | `chore/harness-fase6-api-routes` |
| 7 — Tooling (commitlint+lint-staged+plugins+Renovate+CODEOWNERS) | `23a0f76` (PR #22) | `chore/harness-fase7-lint-strict` |
| 8 — Storybook 10 + 4 stories piloto | _em PR_ | `chore/harness-fase8-storybook` |

A partir da Fase 1, **toda** mudança via branch + PR.

### Decisões técnicas Fase 1

- **Fake Date opt-in**: `installDeterminism({time})` so congela Date se string passada. Default nao congela porque modulos com `Date.now()` top-level (ex: `FUTURE/PAST` em `auth.test.ts`) sao avaliados antes do `beforeEach` e ficariam dessincronizados.
- **Fake timers seletivo**: apenas `Date` e `performance`. `setTimeout/setInterval` reais para nao quebrar `waitFor()` (Testing Library) e `user-event` v14.
- **ESLint base via `eslint-config-next/core-web-vitals + typescript`**, flat config.
- **Regras Next 16/React 19 novas relaxadas para `warn`** (`react-hooks/purity`, `set-state-in-effect`, `exhaustive-deps`, `import/no-anonymous-default-export`). Endurecidas na Fase 7.
- **Husky no monorepo split**: `core.hooksPath = frontend/.husky` setado via `prepare` script.
- **Pre-commit hook**: `typecheck && lint && test`. Bloqueia commit se qualquer falhar.

## 6. Fases

| # | Escopo | Esforço | PR |
|---|--------|---------|----|
| 0 | Limpeza: remove happy-dom, gitignore, jsdom default | 1h | #1 |
| 1 | Setup centralizado + determinismo (time/random/uuid/motion) + mocks globais + ESLint base + husky pre-commit | 6h | #2 |
| 2 | Vitest projects + coverage per-path + API routes habilitada | 4h | #3 |
| 3 | MSW + OpenAPI codegen + factories zod + `renderWithProviders` | 1.5d | #4 |
| 4 | Property-based em zod schemas + utils | 1d | #5 |
| 5 | Migração testes: `vi.mock(axios)` → MSW, co-localização | 2d | #6 |
| 6 | API routes testing (`src/app/api/**`) | 1d | #7 |
| 7 | ESLint + commitlint + husky + lint-staged + Renovate + CODEOWNERS + PR template + release-please | 4h | #8 |
| 8 | Storybook + MSW addon + a11y addon + dark mode stories | 1d | #9 |
| 9 | Playwright base + sharding + network fixtures + auth states | 6h | #10 |
| 10 | Specs E2E críticos (8 grupos, ver §7) | 4d | #11 |
| 11 | A11y (vitest-axe + axe-playwright) + visual + memory leak | 1d | #12 |
| 12 | Lighthouse CI + bundle-analyzer + linkinator | 4h | #13 |
| 13 | Security: audit + osv + gitleaks + ZAP + SBOM + license | 1d | #14 |
| 14 | Mutation testing (Stryker, semanal) | 4h | #15 |
| 15 | Contract testing (Pact + broker + can-i-deploy) | 1d | #16 |
| 16 | Sentry + Web Vitals RUM + source maps | 5h | #17 |
| 17 | CI completo + PR preview deploys (Vercel/CF Pages) | 1.5d | #18 |
| 18 | Observability: Codecov flags + flake tracking + dead code + analytics | 5h | #19 |

**Total**: 20-24 dias úteis (1 dev). 10-12 dias com 2 devs (paralelizar 4-7 e 11-15).

## 7. Specs E2E

`critical/`:
- `auth.spec.ts`
- `admin-aluno-crud.spec.ts`
- `admin-treinador-crud.spec.ts`
- `aluno-treino.spec.ts`
- `checkout-stripe.spec.ts` (test mode + 3DS + declined + webhook via stripe-cli)
- `inactivity.spec.ts`
- `responsive-table.spec.ts`
- `excel-download.spec.ts` (valida planilha gerada via `exceljs`)

`security/`:
- `csp-headers.spec.ts`
- `cookies-security.spec.ts` (HttpOnly, Secure, SameSite)
- `csrf.spec.ts`
- `rate-limit.spec.ts`

`lgpd/`:
- `consent-cookies.spec.ts`
- `delete-account.spec.ts`
- `export-data.spec.ts`

`multi-tab/`: `logout-cross-tab.spec.ts`, `session-sync.spec.ts`

`network/`: `slow3G.spec.ts`, `offline.spec.ts`, `retry.spec.ts`

`a11y/`: `all-pages-axe.spec.ts` (varre rotas por role)

`visual/`: snapshots de páginas-chave, light + dark

`smoke/` (5 specs lightweight, pós-deploy):
- health, login admin, listar alunos, criar 1 aluno + cleanup, checkout 1 plano

## 8. CI workflows

`ci.yml` jobs (paralelos onde possível):

1. setup (cache `node_modules`, `~/.cache/ms-playwright`, `.next/cache`)
2. lint (eslint + tsc + commitlint)
3. test-unit (matrix Node 22 + 24)
4. test-integration
5. test-api
6. test-property
7. storybook-test
8. e2e (matrix `{chromium, firefox, webkit} × {1/4..4/4}` shards)
9. e2e-mobile (apenas push para main)
10. visual
11. a11y
12. lighthouse
13. bundle-size (delta vs main, comenta PR)
14. security (audit + osv + gitleaks + license)
15. codeql
16. sbom (CycloneDX)
17. openapi-drift
18. coverage (merge → Codecov flags por camada)
19. gate (depende de tudo, required check)

Preview deploy: Vercel/CF Pages por PR. E2E e ZAP rodam contra preview URL.

Workflows separados:
- `mutation.yml` — semanal + `workflow_dispatch`
- `contract.yml` — publish + verify Pact
- `smoke.yml` — pós-deploy homolog
- `zap.yml` — DAST baseline
- `sbom.yml` — supply chain
- `release.yml` — release-please ou changesets

## 9. Deps (devDependencies)

```jsonc
{
  // Runners
  "vitest": "^4", "@vitest/coverage-v8": "^4", "@vitest/ui": "^4",
  "@playwright/test": "^1", "@axe-core/playwright": "^4", "vitest-axe": "^1",

  // Property-based + mutation
  "fast-check": "^3", "@fast-check/vitest": "^0",
  "@stryker-mutator/core": "^8", "@stryker-mutator/vitest-runner": "^8",
  "@stryker-mutator/typescript-checker": "^8",

  // Contract + schema
  "@pact-foundation/pact": "^13",
  "openapi-typescript": "^7", "openapi-msw": "^0",

  // Storybook
  "storybook": "^8", "@storybook/nextjs": "^8", "@storybook/test-runner": "^0",
  "@storybook/addon-essentials": "^8", "@storybook/addon-interactions": "^8",
  "@storybook/addon-a11y": "^8", "msw-storybook-addon": "^2",

  // Mocking
  "msw": "^2", "@mswjs/data": "^0", "@faker-js/faker": "^9",

  // Determinismo
  "seedrandom": "^3", "@types/seedrandom": "^3",

  // Quality
  "eslint-plugin-testing-library": "^7", "eslint-plugin-jest-dom": "^5",
  "eslint-plugin-playwright": "^2", "eslint-plugin-security": "^3",
  "@commitlint/cli": "^19", "@commitlint/config-conventional": "^19",
  "husky": "^9", "lint-staged": "^15", "prettier": "^3",

  // Performance
  "@lhci/cli": "^0", "@next/bundle-analyzer": "^16", "linkinator": "^6",

  // Supply chain
  "@cyclonedx/cyclonedx-npm": "^2", "license-checker": "^25",

  // Hygiene
  "knip": "^5", "madge": "^8", "ts-prune": "^0",

  // Observability
  "@sentry/nextjs": "^8", "web-vitals": "^4"
}
```

## 10. Scripts package.json

```json
{
  "scripts": {
    "dev": "next dev",
    "build": "next build",
    "start": "next start",
    "analyze": "ANALYZE=true next build",

    "test": "vitest run",
    "test:unit": "vitest run --project unit",
    "test:integration": "vitest run --project integration",
    "test:api": "vitest run --project api",
    "test:property": "vitest run --include '**/*.property.test.ts'",
    "test:watch": "vitest --project unit",
    "test:coverage": "vitest run --coverage",
    "test:ui": "vitest --ui",
    "test:mutation": "stryker run",
    "test:contract": "vitest run src/test/pact",

    "e2e": "playwright test",
    "e2e:ui": "playwright test --ui",
    "e2e:smoke": "playwright test e2e/specs/smoke",
    "e2e:security": "playwright test e2e/specs/security",
    "e2e:lgpd": "playwright test e2e/specs/lgpd",
    "e2e:update-snapshots": "playwright test --update-snapshots",

    "storybook": "storybook dev -p 6006",
    "storybook:build": "storybook build",
    "storybook:test": "test-storybook --url http://localhost:6006",

    "lint": "eslint . && tsc --noEmit",
    "lint:fix": "eslint . --fix",

    "lhci": "lhci autorun",
    "links": "linkinator out/",
    "knip": "knip",
    "deadcode": "madge --circular src/",
    "license": "license-checker --failOn 'GPL-3.0;AGPL-3.0;LGPL-3.0'",
    "sbom": "cyclonedx-npm --output-file sbom.cdx.json",

    "openapi:fetch": "curl -o openapi.json $BACKEND_OPENAPI_URL",
    "openapi:gen": "openapi-typescript openapi.json -o src/test/msw/types.ts",
    "openapi:check": "npm run openapi:fetch && npm run openapi:gen && git diff --exit-code src/test/msw/types.ts"
  }
}
```

## 11. Princípios

1. Pirâmide saudável: muitos unit + property, alguns integration + contract, poucos E2E.
2. Determinismo absoluto: tempo, random, UUID, motion sempre controlados.
3. Failure clarity: erro localiza arquivo + linha + selector.
4. Co-location: teste perto do código. `src/test/` só infra.
5. Type-safe end-to-end: factories zod, handlers OpenAPI, contracts Pact.
6. CI sub-15min p95: paralelizar, cache, shard agressivos.
7. Reprodutibilidade: lockfile commit, `.nvmrc`, CI runner SHA.
8. Zero bypass: `--no-verify`, `skip-tests` proibidos.
9. Quality > Coverage: mutation score > linha; property > example.
10. Shift-left security: SAST + DAST + supply chain em PR.
11. LGPD by default: specs cobrindo consent, delete, export.
12. Observability obrigatória: Sentry + Web Vitals desde dia 1.
13. Test ownership: CODEOWNERS força review.
14. Contract over snapshot: Pact + zod > snapshots frágeis.
15. No DOM snapshots: `toMatchSnapshot()` em HTML proibido — usar visual regression.

## 12. Riscos + mitigação

| Risco | Mitigação |
|-------|-----------|
| MSW handler desatualiza vs backend | Job `openapi-drift` semanal + fail em PR se schema mudar |
| Playwright flaky em CI | retries=2 + traces + flake tracker; SLA 5% flake máx |
| Visual snapshot drift por fonte/OS | Baselines só Linux CI; lint regra proíbe `--update-snapshots` local |
| Stripe API change | Pin SDK; fallback MSW; contrato Pact opcional |
| Custo CI alto | Shard agressivo, cache, mobile só em main, mutation só semanal |
| Coverage gate bloqueia hotfix | Label `hotfix` libera threshold (mantém testes) |
| Mutation testing lento | Roda semanal, não em PR; score em dashboard Stryker |
| Sentry quota | Replay sampling 10%, erros 100%, alert em quota 80% |
| Pact broker hospedagem | Auto-hospedar Docker em homolog ou pactflow.io free tier |
| Preview deploys custosos | Retenção 7 dias, cleanup automático |
| OpenAPI drift cascata | Backend e frontend versionam contrato; bump = PR coordenado |
| LGPD test data | Faker pt-BR + CPFs sintéticos; zero PII real |
| Memory leak false positive | `--expose-gc` no Chromium; threshold 1.5x conservador |
| Test impact bypass | `vitest --changed` + daily full run de segurança |

## 13. Métricas de saúde

Dashboard semanal (Datadog/Grafana):
- Total testes por camada
- Coverage por camada
- Mutation score
- Flake rate
- Tempo médio CI/PR
- Bundle size delta
- Lighthouse score
- Sentry errors/sessão
- Web Vitals p75
- Bugs em prod sem teste prévio

Metas:
- Mutation score ≥ 75% (lib + hooks)
- Flake rate < 2%
- CI < 15 min p95
- Lighthouse perf ≥ 85
- LCP p75 < 2.5s
- Sentry error rate < 0.5%

## 14. Decisões futuras

1. Chromatic vs Percy ($) — visual hosted vs Playwright free
2. Datadog Test Visibility vs BuildKite ($) — flake tracking
3. PactFlow vs self-hosted Pact broker
4. Vercel vs Cloudflare Pages — preview deploys
5. i18n: adicionar `next-intl` testing se virar multi-locale
6. Mobile native: harness paralelo se houver React Native
7. AI test gen — avaliar Vitest browser mode + Copilot em Q3 2026
