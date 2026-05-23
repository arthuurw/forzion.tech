# Frontend Test Harness — Racional das Fases

Complemento do `frontend-harness-plan.md`. Explica **por que** cada fase existe, **vantagens concretas**, **trade-offs aceitos** e **métricas de sucesso**.

Atualizado a cada fase concluída.

---

## Fase 0 — Limpeza + migração para jsdom

**Status**: concluída (commit `6a3a636`, direto em `homolog` antes da regra de branches).

### Objetivo

Eliminar dívida do estado base e estabelecer DOM env único antes de qualquer expansão.

### Por que

- Suíte tinha **happy-dom + jsdom** instalados ao mesmo tempo. Apenas happy-dom era usado em runtime. Configuração herdada sem propósito, ocupa cache, gera surpresa quando dev assume que `jsdom` é o env real.
- Arquivos `count-tests.cjs` e `test-results.json` versionados eram **artefatos de debug humano** (script ad-hoc + dump de saída), não código de produto. Poluem `git status`, geram diff ruidoso em PRs.
- MUI v9 + Stripe Elements + Recharts dependem de spec DOM completa. happy-dom cobre ~85% do spec; jsdom ~95%. Diferenças aparecem em casos sutis: `getComputedStyle` vazio, transitions, popper positioning.
- Não há `.nvmrc`. CI usa Node 22, mas dev local pode estar em qualquer versão. Bugs silenciosos quando engine diverge (ex: `entities@8` exige Node ≥20.19, quebra em 20.17).

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **Conformidade DOM maior** | jsdom 26 cobre `getComputedStyle`, `HTMLDialogElement`, mais APIs CSS. Reduz falsos negativos em testes de UI MUI. |
| **Convergência com padrão de mercado** | jsdom é o env-default do Vitest + Jest. Devs novos não precisam aprender quirks do happy-dom. |
| **Diff de PR mais limpo** | Artefatos no `.gitignore` evitam ruído visual nas revisões. |
| **Reprodutibilidade entre máquinas** | `.nvmrc` fixa Node 22, evita "funciona aqui mas não no CI". |
| **Setup mais leve** | Menos 5 pacotes (`happy-dom` + deps) no `node_modules`. |

### Trade-offs aceitos

- **jsdom 26 (não 27)**: jsdom 27 exige Node ≥20.19; usuário local em 20.17. Aceitamos 26 (1 versão atrás) até Node ser upgradeado universalmente. Renovate bumpa quando viável.
- **jsdom ~30% mais lento que happy-dom**: irrelevante em suite de 302 testes (4-6s). Em suites de >5000 testes a diferença pesaria; reavaliar então.
- **Quebra cross-realm `Uint8Array`**: `jose.SignJWT` em jsdom usa Web Crypto do realm jsdom, incompat com `instanceof Uint8Array` do Node. Mitigado via diretiva `// @vitest-environment node` por arquivo. Antecipa separação de project `api` (Fase 2).

### Métricas de sucesso

- ✅ 302 testes verdes
- ✅ `tsc --noEmit` limpo
- ✅ Zero referência a `happy-dom` no código
- ✅ `.gitignore` cobre todos artefatos previstos

---

## Fase 1 — Setup centralizado + determinismo + tooling de qualidade

**Status**: concluída (branch `chore/harness-fase1-setup-determinismo`, PR #11).

### Objetivo

Consolidar infraestrutura de testes em estrutura previsível, eliminar fontes de flake derivadas de não-determinismo, e estabelecer gate de qualidade automatizado a partir do **primeiro commit**.

### Por que

#### Setup centralizado

- `src/test/setup.ts` antes da fase era **uma linha** importando `jest-dom`. Sem ponto único pra mocks globais, polyfills, ou configuração compartilhada. Cada teste replicava setup boilerplate.
- jsdom não fornece `matchMedia`, `IntersectionObserver`, `ResizeObserver` — APIs que MUI/Recharts assumem. Sem polyfill, falhas mascaradas surgem em testes de UI responsiva (`useMediaQuery`, virtualization, charts).

#### Determinismo

- `dayjs()`, `new Date()`, `Math.random()`, `crypto.randomUUID()` retornam valores diferentes a cada execução. Testes que renderizam timestamps, IDs ou geram seeds ficam flaky **silenciosamente**: passam local, falham em CI; passam num dia, falham outro.
- Animações MUI (`Fade`, `Slide`, `Collapse`) usam `requestAnimationFrame` + transitions CSS. Em testes, transition pode terminar antes ou depois da assertion — race condition. `prefers-reduced-motion: reduce` instrui MUI a desabilitar animações.
- Plano enterprise exige **mutation testing** (Fase 14). Mutation só faz sentido se cada execução do teste é determinística — caso contrário, mutantes "sobrevivem" por aleatoriedade, distorcendo score.

#### Tooling antecipado

- Usuário pediu validação obrigatória em **todo** commit. Sem husky, dev pode esquecer; sem ESLint base, "lint" não significa nada. Antecipar de Fase 7 para Fase 1 garante que **todo** commit a partir daqui já é validado.
- Padrão constante = previsibilidade. PR review não precisa verificar "rodou testes?" — pre-commit garante.

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **Zero flake por animação MUI** | `forceReducedMotion()` global elimina race conditions de transition. |
| **Reprodutibilidade total opt-in** | `freezeTime("2026-01-01")` + `seedRandom(42)` + `deterministicUuid()` garantem mesma saída em qualquer máquina. |
| **Polyfills automáticos** | Tests novos não precisam stub manual de `matchMedia` etc. Reduz boilerplate por arquivo. |
| **Validação automatizada** | Husky bloqueia commit inválido. Impossível introduzir regressão sem CI explícito. |
| **Workflow padronizado** | Branch por fase + Conventional Commits + pre-commit + PR squash. Sem variação ad-hoc. |
| **Mutation-ready** | Determinismo é pré-requisito de Fase 14 (Stryker). Investimento amortizado. |
| **ESLint Next 16 nativo** | Detecta bugs de hooks (purity, set-state-in-effect) que Next 16/React 19 acabaram de promover a regra. Hoje warning, próximas fases endurecidas. |

### Trade-offs aceitos

- **Fake Date opt-in (não global)**: módulos com `Date.now()` em top-level são avaliados antes do `beforeEach`. Tornar Date global mocada quebrava `auth.test.ts` (variáveis `FUTURE/PAST`). Aceitamos opt-in: testes que querem Date deterministico chamam `freezeTime()` explícito. **Custo**: dev precisa lembrar; **mitigação**: documentar + adicionar regra ESLint custom em Fase 7.
- **`setTimeout/setInterval` reais (não fake)**: Testing Library `waitFor()` e `user-event` v14 dependem de timers reais para polling. Faking-os trava testes em timeout infinito. Mantemos reais por default; quem precisa de timer determinístico ativa via `vi.useFakeTimers({ toFake: ["setTimeout"] })` no próprio teste.
- **Regras Next 16/React 19 como `warn`**: 25 violações pré-existentes em código atual. Promover a `error` agora bloquearia todo commit até refatorar. Aceitamos warn temporário com TODO explícito para Fase 7 (lint completo).
- **Husky em monorepo split**: repo tem `frontend/package.json` mas raiz é solução .NET. Husky 9 espera `package.json` na raiz git. Workaround: `prepare` script faz `cd .. && husky frontend/.husky`. Funciona, mas exige `npm install` em `frontend/` para configurar hook. Mitigação: documentar no README.
- **ESLint flat config**: incompatível com plugins legacy via FlatCompat (erro `circular structure`). Mitigado usando export nativo flat de `eslint-config-next`. Plugins customizados (Fase 7) podem exigir cuidado similar.

### Métricas de sucesso

- ✅ 302 testes verdes após reorganização
- ✅ Pre-commit valida `typecheck && lint && test`
- ✅ 0 erros ESLint (28 warnings rastreados pra Fase 7)
- ✅ `installDeterminism()` aplicado em todo `beforeEach`
- ✅ Estrutura `src/test/setup/` e `src/test/determinism/` em uso
- ✅ Documentação de decisões no plano (`.claude/frontend-harness-plan.md`)

### Impacto futuro

- Fase 2 (Vitest projects): herda `setupFiles` que já está no caminho novo (`src/test/setup/unit.ts`)
- Fase 3 (MSW): handlers usarão factories que serão deterministicas via `seedRandom`
- Fase 4 (property-based): `fast-check` aproveita seed para reproducibilidade
- Fase 11 (a11y): `forceReducedMotion` evita axe-violations falsas em animações
- Fase 14 (mutation): determinismo é pré-requisito; investimento desta fase é pré-pago aqui

---

## Próximas fases

A serem adicionadas à medida que concluídas:

- Fase 2 — Vitest projects + coverage per-path + API routes habilitada
- Fase 3 — MSW + OpenAPI codegen + factories + renderWithProviders
- Fase 4 — Property-based testing
- Fase 5 — Migração testes existentes para MSW
- Fase 6 — API routes testing
- Fase 7 — Lint endurecido + commitlint + lint-staged + Renovate + CODEOWNERS
- Fase 8 — Storybook
- Fase 9 — Playwright base + sharding + network fixtures
- Fase 10 — Specs E2E críticos
- Fase 11 — A11y + visual + memory leak
- Fase 12 — Lighthouse CI + bundle + crawl
- Fase 13 — Security gates
- Fase 14 — Mutation testing
- Fase 15 — Contract testing Pact
- Fase 16 — Sentry + Web Vitals RUM
- Fase 17 — CI completo + PR preview deploys
- Fase 18 — Observability + flake tracking
