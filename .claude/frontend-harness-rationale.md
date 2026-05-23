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

## Fase 2 — Vitest projects + coverage per-path + API routes habilitada

**Status**: concluída (branch `chore/harness-fase2-vitest-projects`).

### Objetivo

Separar a execução de testes em projects isolados conforme o ambiente que cada tipo exige (node ou jsdom), e estabelecer thresholds de cobertura **por camada** que reflitam o valor real de cada área do código.

### Por que

#### Projects separados

- Em Vitest sem projects, todo teste roda no mesmo env (`jsdom`). Testes puros de `lib/`, `hooks/`, validações zod e API routes pagam o custo de inicializar `jsdom` (≈2-5s) sem ganho — eles não precisam de DOM.
- `jsdom` introduz cross-realm mismatch com Node APIs (ex: `Uint8Array` em `jose.SignJWT`). Tests de Route Handlers usam `jose` para JWT — env `node` faz funcionar sem hack.
- Setup específico por env evita carregar polyfills DOM (`matchMedia`, `IntersectionObserver`) em testes que nunca tocam DOM.

#### Coverage per-path

- Coverage global homogêneo penaliza camadas que **deveriam** ter cobertura alta (lógica pura em `lib/`) e nivela por baixo, ou força cobertura alta em camadas onde 100% é inútil (page components Next que são essencialmente JSX layout).
- Camadas diferentes têm tipos de bug diferentes:
  - `lib/`, `hooks/`: bugs lógicos, devem ter cobertura quase total
  - `components/`: bugs de interação, cobertura alta razoável
  - `app/api/`: handlers críticos de segurança, devem ser muito testados
  - `app/` (pages): orquestração; faz sentido cobertura média
- Threshold per-path permite **falhar PR** se cobertura cair em camada crítica, sem bloquear PR que só toca page boilerplate.

#### API routes no coverage

- `src/app/api/**` estava **excluído** de coverage no estado base. Erro grave: API routes são gateway de segurança (auth, rate limit, validação de entrada). Não medir cobertura é cegueira deliberada.
- Habilitar coverage em API routes (mesmo com baseline modesto) força visibilidade. Próximas fases podem mirar 90/85/90/90.

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **Velocidade**: unit roda em ~440ms (138 testes) | env node sem jsdom — 10x mais rápido que o setup unificado |
| **Watch mode focado** | `vitest --project unit` permite TDD sub-segundo sem rodar suite inteira |
| **Paralelismo CPU melhor** | unit usa `threads`, integration usa `forks` (isolamento MSW na Fase 3) |
| **Coverage visível em API routes** | Inclui camada crítica antes invisível |
| **Threshold reflete camada** | Falha em `lib/` (95%) trava PR; falha em `app/` (70%) também — escala diferente |
| **Sem regressão** | 302 testes verdes mantidos; distribuição: unit 138 / integration 156 / api 8 |
| **Determinismo localizado** | unit/api setup minimal; integration carrega jest-dom + polyfills |

### Trade-offs aceitos

- **Manutenção de include lists em `vitest.config.mts`**: tests em `src/test/` (flat herdado) não cabem nos globs convencionais. Listamos cada arquivo por project. Custo elimina-se na Fase 5 (co-localização).
- **`src/lib/utils/excel.test.ts` no project `integration`**: o teste mexe com `document.createElement` (download de planilha). Diverge do padrão "tudo em `src/lib/` é unit". Aceitamos exceção até refatorar.
- **Thresholds Fase 2 baseline (não target)**: cobertura atual de `src/app/api/**` (branches 84%) e `src/app/**` (functions 56%) está abaixo dos targets finais. Aceitamos baseline atual com TODO no código. Apertar acontece nas Fases 5 (testes migrados), 6 (API routes testadas) e 11 (mais E2E).
- **Globs sobrepostos `src/app/api/**` ⊂ `src/app/**`**: o glob `src/app/**` inclui também `api/`. Vitest aplica os dois thresholds independentemente — arquivo precisa passar nos dois. Funciona, mas sutil.

### Métricas de sucesso

- ✅ 3 projects rodando isolados: unit / integration / api
- ✅ 302 testes verdes mantidos (zero regressão)
- ✅ unit project em 441ms (10x ganho)
- ✅ Coverage per-path passa em todas camadas (baseline ajustado)
- ✅ `src/app/api/**` agora coberto (antes excluído)
- ✅ Scripts: `test:unit`, `test:integration`, `test:api`, `test:ui`, `test:watch`
- ✅ Setup em 3 arquivos: `setup/unit.ts`, `setup/integration.ts`, `setup/api.ts`
- ✅ Diretiva `// @vitest-environment node` removida de `api-auth-me.test.ts`

### Impacto futuro

- Fase 3 (MSW): `setup/integration.ts` ganha `server.listen()` sem afetar unit/api
- Fase 5 (migração testes): include lists transitórias desaparecem
- Fase 6 (API routes testing): `createMockRequest()` / `extractCookies()` em `setup/api.ts` já existem
- Fase 11 (a11y): testes a11y de componentes ficam em `integration`
- Fase 14 (mutation): Stryker roda contra unit e api (rápidos) primeiro

---

## Fase 3 — MSW + OpenAPI codegen + factories + renderWithProviders

**Status**: concluída (branch `chore/harness-fase3-msw-openapi`).

### Objetivo

Estabelecer infraestrutura de mock de rede **realista** (intercepta HTTP no nível de fetch/XHR, não substitui módulos), gerar **tipos canônicos** a partir do contrato OpenAPI do backend, e padronizar **dados de teste** via factories e **render com providers**.

### Por que

#### MSW vs `vi.mock("@/lib/api/client")`

- Pattern atual mocka `apiClient` diretamente. Testes verificam que função X chama `apiClient.get("/admin/alunos", ...)` — testam **contrato interno** entre módulos, não que a request HTTP funciona.
- Bug class **invisível**: se `apiClient.get` muda a forma de serializar params (axios → fetch, ou novo interceptor), todos os testes continuam verdes mas produção quebra.
- MSW intercepta no nível de network. Teste fica realista: axios sério monta query string, MSW recebe URL final, valida.
- Mesmo handler funciona em vitest (Node), Storybook (browser) e dev mode (browser worker). Single source of truth.

#### OpenAPI codegen

- Backend .NET 8 expõe spec em `https://homologacao.forzion.tech/swagger/v1/swagger.json` (após PR #14/#15/#16 de infra).
- `openapi-typescript` gera 5464 linhas de tipos tipados. Handlers MSW agora podem importar `paths["/admin/alunos"]["get"]["responses"]["200"]["content"]["application/json"]` e ganhar autocomplete + checagem.
- Job CI futuro (`openapi:check`) regenera os tipos e falha se `git diff` aparecer — backend renomeou campo ou endpoint sem coordenar com frontend.
- Snapshot do `openapi.json` é cache (gitignored); apenas `src/test/msw/types.ts` é versionado.

#### Factories com Faker

- Fixtures literais em testes (`{ id: "1", nome: "X" }`) escondem campos opcionais que produção tem. Quando schema cresce, factory falha em compile time, fixture inline silenciosamente fica obsoleta.
- Factory `buildAluno()` retorna sempre objeto completo (todos os campos requeridos). Tests passam `overrides` apenas para o que importa pro caso.
- Determinismo: factories usam `faker` que respeita `Math.random` seedado (Fase 1). Mesmo input → mesma saída.

#### renderWithProviders

- Sem helper, cada teste replica `<ThemeProvider><AuthProvider><Snackbar>...</Snackbar></AuthProvider></ThemeProvider>` boilerplate. Esquecimento gera bug obscuro.
- Helper centraliza ordem correta dos providers (mesma do `layout.tsx`).
- Opções `skipAuth`, `skipSnackbar` permitem isolar componentes em testes unitários puros sem providers irrelevantes.

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **Tipos canônicos backend↔frontend** | 5464 linhas auto-geradas; renomeio de campo no .NET quebra compile no frontend |
| **Mock de rede realista** | axios serializa query, MSW recebe URL final, valida — pega bugs de cliente HTTP |
| **Reuso de handlers** | Mesma definição em Vitest (Node), Storybook (browser), dev mode (worker) |
| **Override por teste** | `server.use(...)` adiciona handler temporário; `afterEach` reseta automático |
| **Factories tipadas** | `Partial<T>` overrides + defaults completos → tests resistem a expansão de schema |
| **renderWithProviders centraliza setup** | Reduz boilerplate; ordem de providers correta automaticamente |
| **Determinismo preservado** | Faker respeita `Math.random` seedado (Fase 1); mesmas datas/IDs entre runs |
| **PoC validado** | `msw-pilot.test.ts` demonstra padrão end-to-end com apiClient REAL (não mockado) |

### Trade-offs aceitos

- **Coexistência com `vi.mock("@/lib/api/client")`**: 17 arquivos de teste existentes ainda mockam apiClient. Não migramos todos nesta fase — Fase 5 fará migração em massa. MSW server.listen() está ativo mas só intercepta requests que **escapam** dos mocks atuais (zero, pois apiClient é mockado completamente). `onUnhandledRequest: "error"` portanto não dispara erros em testes legacy.
- **OpenAPI snapshot manual**: hoje `openapi:sync` é manual. Job CI `openapi:check` virá com Fase 17. Até lá, dev precisa rodar `npm run openapi:sync` localmente quando backend mudar — risco de drift silencioso.
- **Handlers vazios por área**: criamos arquivos stub (`admin.ts`, `aluno.ts`, etc) com `[]` exportado. Padrão estabelecido, mas valor zero até Fase 5 popular. Aceitamos placeholder para reduzir delta da Fase 5.
- **`buildAluno` etc retornam objeto único**: não há `buildList()` ou `pick from pool`. Pra suítes que precisam coleção, dev itera manualmente. Simples por enquanto; expandir se padrão aparecer.
- **`renderWithProviders` sem Router**: Next.js `app router` é mockado por arquivo via `vi.mock("next/navigation")`. Não tem ainda providers de router no helper. Adicionar quando necessário (provavelmente Fase 5 ou Fase 8 com Storybook).
- **`@mswjs/data` instalado mas não usado**: pacote pra DB in-memory. Será aproveitado em testes mais complexos (CRUD com estado entre requests). Por enquanto, handlers simples retornam fixtures factories.

### Métricas de sucesso

- ✅ Spec OpenAPI baixado de homologação (153 KB)
- ✅ `src/test/msw/types.ts` gerado (5464 linhas tipadas)
- ✅ MSW server lifecycle integrado em `setup/integration.ts`
- ✅ `onUnhandledRequest: "error"` ativo (sem regressão em testes legacy)
- ✅ 5 arquivos de handlers stub (admin/aluno/treinador/pagamento/auth + index)
- ✅ 4 factories: buildAluno, buildTreinador, buildPlano, buildPagamento + buildAssinatura
- ✅ `renderWithProviders()` com opções skipAuth/skipSnackbar
- ✅ Piloto MSW funcional: 3 testes novos cobrindo GET sucesso, query params, erro 500
- ✅ 305 testes verdes (302 + 3 piloto)
- ✅ Scripts: `openapi:fetch`, `openapi:gen`, `openapi:sync`, `openapi:check`

### Impacto futuro

- Fase 4 (property-based): factories geram inputs aleatórios bem-formados; `fast-check` complementa em validators
- Fase 5 (migração testes): substituir `vi.mock("@/lib/api/client")` por `server.use(...)` em 17 arquivos
- Fase 6 (API routes testing): MSW também serve handlers em testes de Route Handlers se necessário
- Fase 8 (Storybook): `msw-storybook-addon` reaproveita handlers
- Fase 11 (a11y): `renderWithProviders` reduz boilerplate em testes axe
- Fase 17 (CI completo): job `openapi:check` detecta drift backend↔frontend

---

## Próximas fases

A serem adicionadas à medida que concluídas:

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
