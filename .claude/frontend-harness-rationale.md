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

## Fase 4 — Property-based testing

**Status**: concluída (branch `chore/harness-fase4-property-based`).

### Objetivo

Adicionar testes que verificam **propriedades universais** (invariantes) sobre funções puras — validadores zod e formatters — usando geração automática de inputs (`fast-check`). Complementa testes baseados em exemplos com cobertura de espaços de input praticamente infinitos.

### Por que

#### Limite do teste por exemplo

- Tests existentes usam exemplos manualmente escolhidos (`expect(emailSchema.parse("a@b.com"))...`). Dev escolhe inputs que **acha** representativos — vieses humanos escondem edge cases.
- Schemas zod e formatters são **funções puras**: same input → same output, sem side effects. Caso ideal para property testing.
- Inputs em validators são essencialmente infinitos. Cobrir "todos" é impossível, mas gerar 100-1000 inputs aleatórios pega classes inteiras de bugs (unicode, vazio, max+1, regex backtracking, format edge cases).

#### Shrinking automático

- Quando `fast-check` encontra contra-exemplo, ele **diminui** (shrink) até o menor caso que reproduz a falha. Bug aparece com input mínimo (ex: `!@a.aa` em vez de `J0hn.D03+spam@my-corp.us`).
- Loga seed da execução → reprodução determinística pra debug.

#### Pega bugs reais imediatamente

- Já nesta fase, `fast-check` **expôs gap real**: `fc.emailAddress()` gera RFC-válidos (ex: `a..b@c.com`, `!@a.com`) que `emailSchema` rejeita. Documentamos como bug de validação (regex zod mais conservadora que RFC) e ajustamos arbitrary pra subconjunto seguro.
- Sem property test, teríamos descoberto isso só em produção, com usuário real.

#### Pré-requisito de mutation testing

- Fase 14 (Stryker) só faz sentido se testes pegam mudanças semânticas. Property tests aumentam drasticamente sensibilidade — mutação que muda comportamento em qualquer input válido é detectada.

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **23 properties em 1.7s** | Cada property roda 100 cenários por default; 2300 assertions efetivas |
| **Bugs encontrados de graça** | Já achou: `fc.emailAddress()` vs `emailSchema` divergem em RFC-válidos não-zod-aceitos |
| **Reprodução determinística** | Seed logado em falha permite reproduzir bug exato |
| **Shrinking** | Bugs reportados com input mínimo (`!@a.aa` em vez de gigante) |
| **Cobertura de classe** | "qualquer string com >= 8 chars passa passwordSchema" cobre infinitos casos |
| **Type-safe arbitraries** | `fc.Arbitrary<T>` valida que generator produz tipo correto em compile time |
| **Determinismo respeitado** | `installDeterminism` (Fase 1) reproduzível; fast-check usa seed próprio integrado |
| **Mutation-ready** | Property tests aumentam sensibilidade pra Fase 14 (Stryker) |

### Trade-offs aceitos

- **Arbitraries customizadas para zod**: `fc.emailAddress()` é demais permissivo (gera RFC casos que zod rejeita). Criamos `safeEmailArb` com subconjunto seguro. **Custo**: arbitrary não cobre toda semântica do schema. **Mitigação**: comentário documenta gap, futura issue pra alinhar zod com RFC.
- **`as unknown as T` em algumas arbitraries**: `fc.record({...})` gera objeto com inferência ampla; type narrowing exigiria type-guards. Aceitamos cast em casos isolados (com `satisfies` em outros).
- **100 runs por property (default)**: balanço velocidade vs cobertura. Bugs raros podem escapar. Pra criticidade alta (auth, pagamento), aumentar `{ numRuns: 1000 }` por property. Configurar global via `fc.configureGlobal` no setup se necessário.
- **Cobertura apenas em `src/lib/`**: hooks puros e utilidades adicionais (em `src/hooks/`, `src/components/*/utils`) ficam pra fases seguintes ou conforme aparecem.
- **`@fast-check/vitest` v0.x**: ainda pre-1.0. API pode mudar. Pin minor; revisar mudanças em Renovate.

### Métricas de sucesso

- ✅ 23 property tests verdes em 1.71s
- ✅ Cobertura: `emailSchema`, `passwordSchema`, `registerPasswordSchema`, `nomeSchema`, `telefoneSchema`, `loginSchema`, `formatarSeries`, `formatarData`, `getWeekLabel`
- ✅ 1 bug real descoberto e documentado (fc.emailAddress vs zod email regex)
- ✅ 328 testes totais verdes (305 + 23 property)
- ✅ Script `test:property` para execução isolada
- ✅ Vitest project `unit` inclui `*.property.test.ts`
- ✅ Coverage exclude já cobre `*.property.test.ts` (testes não contam pra coverage de produção)

### Impacto futuro

- Fase 5 (migração testes): tests migrados podem ganhar variantes property (ex: factory `buildAluno` testada via property que sempre passa schema zod)
- Fase 14 (mutation): Stryker score cresce porque properties pegam mais mutantes que examples-only
- Fase 6 (API routes): handlers podem ser testados via property — payload aleatório → resposta sempre coerente
- Fase 9+ (Playwright): scenarios baseados em property generators (raro mas possível)

---

## Fase 5a — Co-localização de testes

**Status**: concluída (branch `chore/harness-fase5-migracao-msw`).

A Fase 5 original (migração testes → MSW) foi dividida em duas para tornar PRs digestíveis:

- **5a (esta fase)**: move mecânico de arquivos para perto do código testado, sem mudança lógica
- **5b (próxima)**: substituição efetiva de `vi.mock("@/lib/api/client")` por `server.use()` MSW

### Objetivo (5a)

Eliminar a pasta `src/test/` flat (anti-padrão herdado) movendo cada arquivo `*.test.*` para junto do código que ele testa. Sem mudança lógica nos testes — apenas reorganização estrutural.

### Por que

#### Estado base do anti-pattern

- `src/test/` flat continha 16 arquivos de teste misturados com infra (`setup/`, `factories/`, `msw/`, `determinism/`, `render.tsx`).
- Não existe relação visual entre código e teste. Renomeação de arquivo testado deixava o teste órfão silenciosamente.
- `vitest.config.mts` precisava manter listas explícitas de cada arquivo flat distribuídas por project. Inevitável drift quando dev novo adiciona teste e esquece de incluir.

#### Co-localização

- Padrão amplamente adotado em projetos React/Next modernos: teste fica ao lado do módulo testado (`Foo.tsx` + `Foo.test.tsx`) ou em `__tests__/` adjacente.
- Vantagem mecânica: glob `src/lib/**/*.test.ts` pega tudo automaticamente. Zero manutenção em include lists.
- Vantagem cognitiva: PR review vê código + teste no mesmo diff. Esquecer de atualizar teste vira óbvio.

### Mapa de moves

| De `src/test/` | Para |
|---|---|
| `admin-api.test.ts` | `src/lib/api/admin.test.ts` |
| `admin-pages.test.tsx` | `src/app/(admin)/__tests__/admin-pages.test.tsx` |
| `api-auth-me.test.ts` | `src/app/api/auth/me/route.test.ts` |
| `api-auth-route.test.ts` | `src/app/api/auth/route.test.ts` |
| `auth-context.test.tsx` | `src/lib/auth/context.test.tsx` |
| `auth.test.ts` | `src/lib/auth/helpers.test.ts` (testa helpers de auth/middleware) |
| `components.test.tsx` | `src/components/__tests__/components.test.tsx` |
| `formatting.test.ts` | `src/lib/utils/formatting.test.ts` |
| `middleware.test.ts` | `src/middleware.test.ts` |
| `msw-pilot.test.ts` | `src/lib/api/admin.msw.test.ts` (vira referência permanente do padrão) |
| `pagamento-cartao.test.tsx` | `src/components/pagamento/PagamentoCartao.test.tsx` |
| `pagamento.test.tsx` | `src/app/(aluno)/__tests__/pagamento.test.tsx` |
| `rateLimit.test.ts` | `src/lib/rateLimit.test.ts` |
| `responsive-table.test.tsx` | `src/components/ui/__tests__/responsive-table.test.tsx` |
| `useInactivity.test.ts` | `src/hooks/useInactivity.test.ts` |
| `validations.test.ts` | `src/lib/validations/common.test.ts` |

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **Includes canônicos** | `vitest.config.mts` perde 17 entradas transitórias; usa só globs |
| **Zero drift include** | Novo teste em `src/lib/foo.test.ts` é pego automaticamente |
| **Diff de PR mais óbvio** | Código + teste no mesmo file tree path = mesma janela de review |
| **`src/test/` purificado** | Agora só infra (`setup/`, `factories/`, `msw/`, `determinism/`, `render.tsx`) |
| **Sem regressão** | 328 testes verdes mantidos (19 arquivos, mesma contagem da Fase 4) |
| **Coverage exclude expandido** | `**/__tests__/**` adicionado ao coverage exclude para não contar `__tests__` dirs como produto |

### Trade-offs aceitos

- **Parênteses em paths Next route groups**: globs picomatch interpretam `(admin)` como alternation. Usamos padrão mais amplo `src/app/**/__tests__/*.test.tsx` em vez de listar cada route group. Funciona desde que convenção `__tests__/` seja respeitada.
- **`src/test/setup/api.ts` ainda referenciado por testes movidos sem renomear**: imports relativos `./msw/server` no `admin.msw.test.ts` mudaram para alias `@/test/msw/server`. Pattern dali em diante: usar alias `@/test/...` para infra cross-module.
- **`helpers.test.ts` não-canonical**: arquivo testa funções de dois módulos diferentes (`middleware.extractTipoConta` + `context.homeRouteFor`). Mantido como nome genérico em `src/lib/auth/` porque dividir gera fricção sem ganho. Pode renomear na Fase 5b se ajustes lógicos forem feitos.
- **`admin.msw.test.ts` em `src/lib/api/`**: piloto MSW que será modelo para migração 5b. Path destacado (`.msw.test.ts`) ajuda buscar referência rápida. Pode ser consolidado em `admin.test.ts` se migração 5b unificar pattern.

### Métricas de sucesso

- ✅ 16 arquivos movidos para co-localização
- ✅ `src/test/` agora contém apenas infra (sem `*.test.*`)
- ✅ `vitest.config.mts` sem include lists transitórias
- ✅ 328 testes verdes (zero regressão)
- ✅ Distribuição por project: unit 161 / integration 159 / api 8
- ✅ `npm run validate` passa (tsc + lint + test)

### Impacto futuro

- Fase 5b (próxima): substituição de `vi.mock("@/lib/api/client")` por MSW handlers. Agora cada teste está perto do módulo testado, facilitando ver qual handler é necessário.
- Fase 6 (API routes): `src/app/api/auth/me/route.test.ts` já está no padrão final. Novos route handlers seguem mesma convenção.
- Fase 11 (a11y): testes a11y co-localizados (`Foo.a11y.test.tsx` ao lado de `Foo.tsx`) são triviais agora.
- Fase 14 (mutation): Stryker mapeia código ↔ testes via convenção colocated automaticamente.

---

## Fase 5b — Migração de testes auth/fetch para MSW

**Status**: concluída (branch `chore/harness-fase5b-msw-migration`).

### Objetivo

Eliminar o anti-pattern crítico restante (`stubFetch` global em `context.test.tsx`) substituindo por MSW handlers. Após análise dos demais testes, **delimitar o escopo da migração**: nem todo `vi.mock(...)` é anti-pattern.

### Por que (e por que NÃO migrar tudo)

Auditoria dos testes que tocam API revelou três categorias distintas:

| Categoria | Exemplo | É anti-pattern? | Ação |
|-----------|---------|----------------|------|
| **fetch global stubado** | `context.test.tsx` (`vi.stubGlobal("fetch", ...)`) | **Sim** | **Migrar para MSW** |
| **Contract test do API adapter** | `admin.test.ts` (mocka `apiClient` ao testar `adminApi`) | **Não** | Manter — testa shape de URL/params do adapter |
| **Mock de módulo API em component test** | `admin-pages.test.tsx`, `pagamento.test.tsx`, `PagamentoCartao.test.tsx` (mocka `@/lib/api/admin`, `@/lib/api/pagamento`) | **Não** | Manter — boundary aceitável em testes de componente |

#### Por que `admin.test.ts` NÃO migra

- Esse arquivo testa **o próprio adapter** (`adminApi`): cada `it` verifica que `adminApi.listAlunos()` chama `apiClient.get("/admin/alunos", { params: ... })`.
- O valor é detectar typo de URL ou shape de query — **bug class diferente** do que MSW pega.
- Migrar transforma 96 contract tests em integration tests redundantes (admin.msw.test.ts já demonstra integration). Perderíamos granularidade.
- Pattern correto: **dois níveis** — adapter contract tests (mocka apiClient) + integration tests do consumidor (MSW + apiClient real).

#### Por que component tests NÃO migram

- `admin-pages.test.tsx` mocka `@/lib/api/admin` para isolar UI da camada de dados. **Não mocka HTTP** — mocka boundary do component.
- Migrar para MSW exigiria reescrever centenas de linhas e tornaria testes de componente acoplados ao shape de resposta do backend real. Custo alto, valor incremental baixo.
- Manter este pattern em componentes é **convenção React/Next padrão**.

#### Por que `context.test.tsx` migra

- Original: `vi.stubGlobal("fetch", vi.fn().mockImplementation(...))`. **Substitui fetch global** — mock invasivo, não isolável por rota.
- Cada teste configurava sequência de respostas via array indexado. Frágil: se ordem de fetches mudar, teste quebra silenciosamente.
- MSW: handler por rota (`http.get("*/api/auth/me", ...)`, `http.post("*/api/auth/logout", ...)`). Realista, override por teste com `server.use()`, reset automático no `afterEach`.

### Mudanças

#### `src/test/msw/handlers/auth.ts`

Saiu de stub vazio para handlers default:
```ts
http.get("*/api/auth/me", () => HttpResponse.json(null, { status: 401 })),
http.post("*/api/auth/logout", () => HttpResponse.json({ ok: true })),
```

Default = "não autenticado" (401). Testes que precisam sessão ativa usam `server.use()` com override.

#### `src/lib/auth/context.test.tsx`

Reescrito: 141 linhas de `stubFetch + array de respostas` viraram MSW handlers por teste. 6 tests verdes:
- `GET /api/auth/me` sucesso/401/network-error
- `login()` seta tipoConta
- `logout()` chama POST + redirect (verifica handler invocado, sem precisar spy em fetch)
- `useAuth` fora do provider lança erro

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **Realismo** | fetch real → MSW intercepta na network layer (igual produção) |
| **Override por rota, não ordem** | Pre-existia bug latente: stub array indexado quebrava se ordem mudasse |
| **Sem `vi.stubGlobal`** | Mais limpo; sem efeitos colaterais cross-test |
| **Handlers reutilizáveis** | Próximos tests que tocam `/api/auth/*` já têm default; só override se precisar |
| **Verificação de chamada por handler** | Em logout, contamos invocações via closure no handler (mais explícito que `expect(fetch).toHaveBeenCalledWith(...)`) |
| **Sem regressão** | 328 testes verdes mantidos |

### Trade-offs aceitos e re-escopo

- **Re-escopo deliberado de "migrar todos `vi.mock`"** para "migrar fetch global stub": auditoria identificou que outros usos de `vi.mock` testam contratos de adapter ou isolam boundary de componente. Migrar todos seria **trabalho mecânico sem ganho real** — testes ficariam mais complexos sem detectar mais bugs.
- **`admin.test.ts` permanece com `vi.mock("@/lib/api/client")`**: contract test do adapter. Decisão documentada — futura "Fase 6+" ou refactor pode consolidar com `admin.msw.test.ts`, mas o valor atual justifica manutenção.
- **Component tests mantêm `vi.mock("@/lib/api/<area>")`**: boundary em testes de componente é convenção; migrar perde valor.
- **Handler `/api/auth/me` default retorna 401**: pode surpreender em testes futuros. Documentar via comentário no handler. Tests que precisam de auth ativo aplicam `server.use()`.

### Métricas de sucesso

- ✅ `vi.stubGlobal("fetch", ...)` eliminado de toda a suite
- ✅ `context.test.tsx` reescrito com MSW (6 tests verdes)
- ✅ Handler `auth.ts` populado com defaults úteis
- ✅ 328 testes verdes mantidos
- ✅ Validação cross-camada: tsc + lint + 3 projects vitest

### Impacto futuro

- Fase 6 (API routes): handlers de auth já existem como defaults; tests novos compõem via `server.use()`
- Fase 10 (E2E Playwright): mesmo handler MSW pode ser reaproveitado em modo dev/storybook
- Fase 11 (a11y): tests de páginas autenticadas usam `server.use(http.get(/api/auth/me, json(user)))` consistentemente
- Eventual refactor: se valer, consolidar `admin.test.ts` + `admin.msw.test.ts` em padrão único quando outros adapters migrarem

---

## Fase 6 — API routes testing completo

**Status**: concluída (branch `chore/harness-fase6-api-routes`).

### Objetivo

Cobrir os **6 Route Handlers** sem testes do estado base (`logout`, `register/aluno`, `register/treinador`, `treinadores`, `treinadores/[id]/pacotes`, `backend/[...path]`) e atingir o **target final** de cobertura em `src/app/api/**` (90/85/90/90).

### Por que

#### Anti-pattern do estado base

- 2 dos 8 route handlers tinham testes (`auth/route.ts`, `auth/me/route.ts`). 6 estavam sem cobertura.
- API routes do Next.js são **gateway de segurança crítico**: rate limit, auth, sanitização, proxy. Não testar é cegueira deliberada — bugs aqui vazam pra produção como vulnerabilidades.
- `backend/[...path]/route.ts` é proxy completo (path traversal sanitization, header allowlist, Bearer injection, body forwarding). Cada um desses controles precisa teste explícito.

#### Reuso da infra MSW (Fase 3 + 5b)

- Route handlers fazem `fetch` para backend `.NET`. MSW intercepta isso com `server.use()`.
- `setup/api.ts` agora inicia `server.listen()` (Fase 3 só ativava em integration). Decisão antecipada: project `api` também ganha lifecycle MSW.
- `createMockRequest()` (já existia) expandido com `nextUrl`, `arrayBuffer`, `headers` Headers nativos para suportar handlers que usam essas APIs.

#### Migração colateral

- `auth/route.test.ts` original usava `vi.stubGlobal("fetch", ...)` + `vi.mock("next/server")` complexo. Habilitar MSW no project `api` quebrou esse pattern (MSW detectou fetches "não cobertos").
- Aproveitamos para **migrar `auth/route.test.ts` para MSW** consistentemente. Simplificou de 80 linhas + mock manual de `NextResponse` para 100 linhas com handlers `server.use()` + `createMockRequest()`.
- Bug latente eliminado: anteriormente verificava `expect(setCookie).toContain(...)` apenas em 1 cenário. Novo: 4 cenários incluindo HttpOnly/Secure/SameSite + propagação de erro backend.

### Testes adicionados

| Route | Tests | Cenários cobertos |
|-------|-------|-------------------|
| `auth/route.ts` (login) | 4 (era 2, +2 novos) | sem token em body / cookie HttpOnly+Secure+SameSite / propagação erro 401 / rate limit |
| `auth/logout/route.ts` | 3 | com token / sem token / backend falha (cookies ainda limpos) |
| `auth/register/aluno/route.ts` | 3 | rate limit 429 / sucesso 201 / propagação 400 |
| `auth/register/treinador/route.ts` | 3 | idêntico ao aluno |
| `auth/treinadores/route.ts` | 2 | lista do backend / erro 500 |
| `auth/treinadores/[id]/pacotes/route.ts` | 2 | repassa id no URL / 404 |
| `backend/[...path]/route.ts` (proxy) | 10 | path traversal `..` / `.` / valido / Bearer injection / sem token / header allowlist (content-type, accept; bloqueia cookie, x-forwarded-for) / 4 métodos HTTP / propaga status / propaga Content-Type |

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **Cobertura security-critical** | path traversal explicitamente testado; header allowlist explicitamente testada |
| **Target final atingido** | `src/app/api/**`: 90L / 85B / 90F / 90S (era baseline 85/80/90/85) |
| **355 testes verdes** (era 328) | +27 tests novos sem regressão |
| **Project `api` independente** | MSW lifecycle isolado de `integration`; tests API rodam em <1s |
| **Bearer injection coberto** | Logout E proxy testados — antes só inferência via E2E |
| **Header allowlist regressão-proof** | Adicionar header ao allowlist sem teste falha o teste cookie/xff |
| **CreateMockRequest reusável** | Helper expandido com nextUrl + arrayBuffer atende qualquer route handler futuro |

### Trade-offs aceitos

- **`vi.mock("next/headers")` por arquivo**: cada teste que usa `cookies()` precisa mockar o módulo. Helper centralizado seria possível mas adiciona indireção. Aceitamos repetição mínima.
- **`onUnhandledRequest: "error"` em `api`**: pode surpreender quando teste adiciona route handler novo sem stub MSW. Default deliberado — força explicitness. Custo: mais setup por teste; benefício: fetches não previstos viram falha óbvia.
- **Não cobertos**: routes `(public)`, `_landing` (não são API routes, são page components). Cobertura completa de `src/app/**` virá nas Fases 11 (E2E) e 9 (Storybook).
- **Header `cookie` allowlist**: regra explicitamente NÃO repassa `cookie` do cliente (segurança — backend usa Bearer). Teste protege esse contrato.

### Métricas de sucesso

- ✅ 8 dos 8 route handlers cobertos (era 2)
- ✅ Coverage `src/app/api/**` atinge target final 90/85/90/90
- ✅ 355 testes verdes (era 328 antes da Fase 6)
- ✅ Distribuição: unit 161 / integration 159 / api 35
- ✅ `setup/api.ts` ganha MSW lifecycle (server.listen, resetHandlers, close)
- ✅ `createMockRequest` expandido com `nextUrl`, `arrayBuffer`, Headers nativos
- ✅ `auth/route.test.ts` migrado de stubGlobal+complexMock → MSW + createMockRequest
- ✅ Path traversal + header allowlist + Bearer injection cobertos explicitamente

### Impacto futuro

- Fase 11 (E2E security specs): tests Playwright validam CSP/cookies em integração, complementando esses unit tests
- Fase 14 (mutation): Stryker vai stressar handlers — esperamos alto mutation score graças à granularidade
- Refactor de proxy: qualquer mudança em allowlist quebra teste — mudança consciente requer atualizar teste primeiro
- Próximas routes (rate limit endpoint, webhooks Stripe etc): pattern estabelecido — criar route + 1 arquivo `route.test.ts` colocated

---

## Fase 7 — Tooling: commitlint + lint-staged + ESLint plugins + Renovate + CODEOWNERS + PR template

**Status**: concluída (branch `chore/harness-fase7-lint-strict`).

### Objetivo

Implementar a **camada de tooling** prevista no plano original (commitlint, lint-staged, plugins extras, Renovate, CODEOWNERS, PR template). Re-escopo deliberado: **adiar endurecimento de regras Next 16 / React 19 para fase futura de "lint hardening"** (requer refactor de hooks).

### Por que

#### Commitlint

- Conventional Commits já era convenção de facto neste projeto. Sem enforcement, dependia de disciplina humana.
- `commit-msg` hook valida mensagem ANTES de criar commit. Mensagem fora do padrão = commit rejeitado.
- `scope-enum` restrito a `[frontend, backend, infra, ci, deps, tests, docs]`: previne typos (`feature` em vez de `feat`, `crontab` em vez de `ci`).

#### lint-staged

- Antes: pre-commit rodava `npm run lint` em **todo o projeto** (5-10s).
- Agora: lint-staged roda `eslint --fix` apenas em arquivos **staged** (sub-segundo na maioria dos commits).
- Bonus: `--fix` aplica autofix automático em mudanças do dev (jest-dom matchers, formatting, etc) — menos atrito.

#### eslint-plugin-jest-dom + testing-library

- 117 ocorrências do anti-pattern `expect(x).not.toBeNull()` que `prefer-in-document` recomenda como `expect(x).toBeInTheDocument()`.
- 40 ocorrências do anti-pattern `await waitFor(() => expect(getBy...))` que `prefer-find-by` recomenda como `await findBy...`.
- **Auto-fix corrigiu 171 dos 198 warnings**. Dev não precisou intervir manualmente.
- Restantes (24× `set-state-in-effect`, 1× `purity`, etc) requerem refactor lógico — adiados.

#### eslint-plugin-security

- Heurísticas para `eval`, `child_process`, regex backtracking etc.
- `detect-object-injection` **desligado globalmente**: heurística ruim (gera false positive em qualquer `obj[key]` legítimo, incluindo factories e mocks).
- Demais regras ficam ativas — caso surja `eval` ou regex perigosa, é capturado.

#### Renovate (vs Dependabot)

- Dependabot é integrado ao GitHub mas tem config limitada (sem auto-merge granular, sem grouping flexível).
- Renovate:
  - **Auto-merge** para patch/pin/digest (sem revisão humana — atualizações de baixo risco)
  - **Grouping**: Playwright + Storybook + Vitest + MSW + Next.js + React + MUI + Stripe agrupados → 1 PR por área, não 1 por pacote
  - **`minimumReleaseAge: 7 days`** em majors (evita pegar releases bugadas)
  - **Lock file maintenance** semanal automerge
  - Schedule weeknights em America/Sao_Paulo

#### CODEOWNERS

- Não há "review obrigatório" enforcement no repo, mas CODEOWNERS gera review request automático.
- Paths críticos (test infra, husky, configs, CI workflows, nginx, appsettings) atribuídos ao maintainer.

#### PR template

- Checklist visual em cada PR — força reviewer e autor a confirmar testes, security checks, conventional commit.
- Reduz "merged em 10s sem ninguém olhar".

### Trade-offs aceitos

- **Endurecimento de regras hooks adiado**: 24× `set-state-in-effect`, 1× `purity`, 1× `exhaustive-deps`, 1× `import/no-anonymous-default-export`. Refactor exige análise caso-a-caso de cada hook. Decisão consciente: tooling primeiro, refactor em fase isolada ("lint hardening"). Regras ficam como `warn` — visíveis mas não bloqueantes.
- **`security/detect-object-injection` off**: 30 falsos positivos. Heurística não distingue `arr[i]` (seguro) de `obj[userInput]` (perigoso) sem análise de fluxo. Custo > benefício.
- **`lint-staged` apenas em `.{ts,tsx}`**: arquivos JSON/MD/YAML ignorados. Prettier completo virá em fase futura.
- **`release-please` / `changesets` não adotado nesta fase**: projeto ainda não tem release pipeline. Adicionar prematuro. Volta no escopo quando houver releases versionadas.

### Mudanças

#### Arquivos novos

- `frontend/commitlint.config.mjs` — `@commitlint/config-conventional` + scope-enum
- `frontend/.husky/commit-msg` — invoca commitlint na mensagem do commit
- `frontend/.lintstagedrc.json` — `eslint --fix` em `.{ts,tsx}` staged
- `renovate.json` — config completa com grouping + auto-merge patch
- `.github/CODEOWNERS` — paths críticos atribuídos ao maintainer
- `.github/pull_request_template.md` — checklist obrigatório

#### Arquivos atualizados

- `frontend/eslint.config.mjs`:
  - Adiciona `eslint-plugin-jest-dom` (apenas em arquivos de teste)
  - Adiciona `eslint-plugin-testing-library` (apenas em arquivos de teste)
  - Adiciona `eslint-plugin-security` (global, com `detect-object-injection` off)
  - Regras novas configuradas como `warn` (não bloqueante)
  - `ignores` inclui `openapi.json` e `src/test/msw/types.ts` (gerados)
- `frontend/.husky/pre-commit`: agora roda `lint-staged` em vez de `npm run lint` completo

#### Auto-fix em massa

- `npm run lint:fix` corrigiu **171 dos 198 warnings**:
  - `expect(x).not.toBeNull()` → `expect(x).toBeInTheDocument()`
  - `expect(x.textContent).toBe(y)` → `expect(x).toHaveTextContent(y)`
  - `expect(button.disabled).toBe(true)` → `expect(button).toBeDisabled()`
  - `await waitFor(() => expect(getByX))` → `await findByX`
- 355 testes verdes após autofix (zero regressão lógica).

### Métricas de sucesso

- ✅ `commitlint` ativo via `.husky/commit-msg`
- ✅ `lint-staged` no pre-commit (lint só de staged, sub-segundo)
- ✅ 3 plugins ESLint novos (testing-library, jest-dom, security)
- ✅ Auto-fix corrigiu 171 issues sem refactor manual
- ✅ Renovate configurado (auto-merge patch + 8 groupings + 7-day delay em majors)
- ✅ CODEOWNERS em paths críticos
- ✅ PR template obrigatório
- ✅ 355 testes verdes mantidos (zero regressão)
- ✅ 27 warnings restantes (era 28; auto-fix + cleanup)

### Impacto futuro

- "Lint hardening" (fase isolada futura): refactor dos 27 warnings restantes
- Próximas fases: PRs nascem com template + commits validados + lint-staged rápido
- Renovate começa a abrir PRs automaticamente (após admin ativar no GitHub App)
- CI eventual pode adicionar `commitlint` em pull_request action (defesa em profundidade)

---

## Fase 8 — Storybook 10 + 4 stories piloto

**Status**: concluída (branch `chore/harness-fase8-storybook`).

### Objetivo

Estabelecer catálogo de componentes com Storybook 10 (compatível Next 16 + React 19), integrado a MSW para mock de rede e addon a11y para feedback inline de acessibilidade.

### Por que

#### Versão Storybook 10 (não 8)

- Plano original previa Storybook 8 (`@storybook/nextjs@^8`).
- Storybook 8 peer requer `next@^13.5 || ^14 || ^15` — incompatível com Next 16 que o projeto usa.
- Storybook 10.4 suporta `next@^14.1 || ^15 || ^16` e React 19. Migração de versão obrigatória.
- Estrutura mudou: addons `essentials` + `interactions` foram consolidados no core; só precisamos `addon-a11y` explícito.

#### MSW addon

- `msw-storybook-addon` reutiliza os mesmos handlers que MSW server (`src/test/msw/handlers/`). Single source of truth.
- Stories que renderizam componentes que fazem fetch (ex: AuthProvider) ganham mock automático sem reimplementar.
- Service Worker gerado em `public/mockServiceWorker.js` via `npx msw init public/`.

#### Theme + providers

- `preview.tsx` envolve toda story em `ThemeProvider` MUI v9 — stories veem o tema do projeto, não MUI default.
- Decisão: **não envolver em AuthProvider/SnackbarProvider por padrão**. Stories que precisam ativam via decorator local. Razão: stories são unidade isolada; auth/snackbar leak entre stories.

#### Stories piloto

4 componentes UI puros sem deps externas escolhidos como piloto:
- `StatusChip` — 4 stories (3 estados + tamanho medium)
- `AlertBanner` — 6 stories (4 severities + sem título + fechado)
- `LoadingSpinner` — 2 stories (inline + fullPage)
- `EmptyState` — 2 stories (com/sem ação)

Total: **14 stories** demonstrando o padrão.

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **Catálogo visual versionado** | Cada componente UI tem estados documentados como código |
| **A11y feedback inline** | `addon-a11y` mostra violações axe direto no painel da story |
| **MSW reuso** | Stories que fazem fetch usam mesmos handlers dos testes integration |
| **Type-safe stories** | `Meta<typeof Component>` + `StoryObj` garantem tipagem em args |
| **`autodocs` automático** | Tag gera documentação Markdown a partir de tipos TS + JSDoc |
| **`fullscreen` layout** | LoadingSpinner.FullPage usa parameter `layout: "fullscreen"` |
| **Build em CI (Node 22)** | Apesar de exigir Node ≥20.19 (>local 20.17), CI tem 22 |

### Trade-offs aceitos

- **Storybook 10 em vez de 8**: peer dep Next 16 forçou. Documentação online ainda predominantemente Storybook 8 — gap de aprendizado pequeno (sintaxe stories quase idêntica).
- **Build local bloqueado em Node 20.17**: Storybook 10 exige 20.19+ ou 22.12+. Dev local não builda nem dev server roda até upgrade. CI usa Node 22 (`.nvmrc`), funciona sem mudança. Decisão: aceitar — upgrade local é responsabilidade do dev.
- **Sem AuthProvider/SnackbarProvider default em preview**: stories isoladas. Componentes que precisam aplicam via decorator local. Trade-off contra `renderWithProviders` que envolve tudo por default (contexto diferente: renderWithProviders é pra testes integration, providers globais).
- **Apenas 4 componentes story'd nesta fase**: cobertura inicial. Pattern estabelecido; outras stories evoluem incrementalmente conforme componentes mudam.
- **`storybook:test` (test-runner) não rodado nesta fase**: requer servidor Storybook ativo (`storybook dev`). Será exercitado em CI quando setup completo (Fase 17).

### Mudanças

#### Arquivos novos

- `.storybook/main.ts` — config Storybook (framework Next, addon a11y, autodocs)
- `.storybook/preview.tsx` — providers (ThemeProvider) + MSW loader + a11y parameter
- `public/mockServiceWorker.js` — Service Worker MSW (gerado via `npx msw init`)
- 4 arquivos `*.stories.tsx` em `src/components/ui/`

#### Scripts package.json

- `storybook` — `storybook dev -p 6006`
- `storybook:build` — produção estática
- `storybook:test` — test-runner contra servidor rodando

#### Deps novas

- `storybook@^10`
- `@storybook/nextjs@^10` (framework adapter)
- `@storybook/test-runner` (test runner CLI)
- `@storybook/addon-a11y@^10`
- `msw-storybook-addon` (integração handler MSW)

### Métricas de sucesso

- ✅ Storybook 10.4 instalado + configurado
- ✅ Framework `@storybook/nextjs` integrando Next 16 + React 19
- ✅ MSW addon ativo com handlers de `src/test/msw/`
- ✅ A11y addon ativo
- ✅ 14 stories piloto em 4 componentes
- ✅ `npm run validate` passa (tsc + lint + 355 testes)
- ✅ Pre-commit não bloqueia (lint-staged não toca stories)

### Impacto futuro

- Próximas adições de componente devem trazer `.stories.tsx` por convenção
- Fase 11 (a11y dedicada): expandir cobertura axe nas stories + CI gate
- Fase 17 (CI completo): job `storybook-test` builds estático + roda test-runner contra ele
- Eventual: Chromatic ou Percy para visual regression hospedado (avaliação Fase 19+)
- Stories complexas (Forms com FormProvider, Pagination, Tables): decorators wrapper especializados

---

## Fase 9 — Playwright base + sharding + network fixtures + auth states

**Status**: concluída (branch `chore/harness-fase9-playwright`).

### Objetivo

Estabelecer infraestrutura E2E completa: Playwright configurado com 5 projects (3 desktop + 2 mobile), project "setup" gerando storage states por role, fixtures custom para auth/network/console, POM base e utils placeholders. **Sem** specs reais — Fase 10 popula `critical/`, `security/`, `lgpd/`, `multi-tab/`, `network/`, `a11y/`, `visual/`.

### Por que

#### Playwright (não Cypress/WebdriverIO)

- Playwright já era a escolha do plano original (§3). Razões: multi-browser nativo (Chromium + Firefox + WebKit), trace viewer, auto-wait, parallelism + sharding first-class, fixtures tipadas.
- Cypress permanece single-browser por padrão e cobra plano para sharding. WebdriverIO é mais verboso e tem ergonomia inferior para Next.js app router.
- Trace viewer do Playwright (`trace: "on-first-retry"`) é diferencial: PR com flake reproduzível em 1 clique.

#### Project "setup" + storage state

- Pattern oficial Playwright para E2E autenticado: roda uma vez antes dos specs, faz login via `request.post()`, persiste `storageState`. Specs subsequentes carregam estado pronto — login não é repetido N vezes.
- Reduz tempo de E2E suite ~5-10x quando há muitos specs autenticados (sem isso, cada teste faz login UI).
- 3 roles do produto (admin/aluno/treinador) = 3 storage states independentes. Spec opta via `useAuthRole(test, "admin")`.

#### Credenciais via env + skip elegante

- Backend de homologação tem usuários reais. Credenciais não vão pro repo (LGPD + segurança básica).
- `auth.setup.ts` lê `E2E_<ROLE>_EMAIL/PASSWORD`. Se ausente, `test.skip()` com mensagem clara.
- Permite: (a) CI rodar sem secrets (smoke piloto + lint dos specs); (b) dev local rodar contra mocks ou credenciais próprias.
- Fase 10 popula CI com secrets reais.

#### Network fixtures via CDP

- `applyNetwork()` usa Chrome DevTools Protocol (`Network.emulateNetworkConditions`) — único modo confiável de simular slow3G/offline no Playwright.
- Limitação: CDP só funciona em Chromium. Helpers no-op em Firefox/WebKit (log warning). Specs que precisam network throttling devem `test.skip(browserName !== "chromium")`.
- `flakyRoute()` complementa: simula falhas intermitentes via `page.route()` — funciona em todos browsers, valida retry/backoff de `apiClient`.

#### Fixture `consoleErrors`

- `page.on("console")` capturado em fixture reutilizável. Spec chama `consoleErrors.assertNoErrors()` para validar zero erros JS no console durante a interação.
- Pega regressões silenciosas (warnings React, Sentry, MUI deprecations) que UI tests não detectariam.

#### POM em `e2e/pages/`

- Padrão Page Object Model: classe encapsula seletores + ações de uma rota. Spec foca no "o que" (clica login, valida home), não no "como" (seletor exato do botão).
- Fase 9 só define `BasePage` abstract — Fase 10 popula `LoginPage`, `AdminAlunoPage`, etc, conforme specs forem escritos.

#### Utils placeholders (Stripe/CSP/memory/seed)

- Estabelecem **interface** que specs futuros consomem. Stubs documentam o contrato e impedem improvisação ad-hoc na Fase 10.
- `stripe.ts` lista cartões test-mode oficiais — checkout spec não copia números de Stack Overflow.
- `assert-csp.ts` parsea header CSP — security specs validam diretivas obrigatórias.
- `memory.ts` expõe `measureHeap/forceGC` — memory leak specs (Fase 11) já têm API canônica.
- `seed.ts` é **stub que lança erro** — decisão consciente de Fase 10 (test-only API vs fixtures).

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **5 projects prontos** | chromium/firefox/webkit desktop + Pixel5/iPhone13 mobile, todos com `dependencies: ["setup"]` |
| **Sharding first-class** | `--shard 1/4` funciona out-of-the-box; CI Fase 17 só precisa matrix |
| **Auth zero-overhead** | Storage state cache; specs autenticados não re-fazem login |
| **Network throttling realista** | CDP + presets slow3G/fast3G/offline; flakyRoute para retry tests |
| **Console errors gate** | Fixture captura `console.error` automaticamente; spec opta via `assertNoErrors()` |
| **Reporter dual** | CI: html + junit + github + blob; local: html + list |
| **TypeScript end-to-end** | `tsconfig.e2e.json` herda config base; tsc --noEmit cobre e2e/ no validate |
| **ESLint plugin Playwright** | `no-networkidle`, `no-focused-test`, `no-skipped-test` em warn — pega anti-patterns |
| **Smoke piloto verde** | `health.spec.ts` valida infra com tag `@smoke` |
| **README dedicado** | `e2e/README.md` documenta quick-start, env vars, estrutura, pattern de spec |

### Trade-offs aceitos

- **Sem `webServer` no playwright.config.ts**: dev local precisa `npm run dev` em terminal separado, ou `E2E_BASE_URL` apontando pra homolog. CI Fase 17 sobe servidor em job dedicado. Razão: Next 16 dev server tem startup ~5s; integrar em Playwright causa flakes em workers paralelos.
- **CDP só em Chromium**: helpers de network em Firefox/WebKit são no-op com warning. Aceitamos — slow3G/offline são specs de feature, não regressão cross-browser. Mobile-safari mais relevante para visual/responsive.
- **`seed.ts` lança erro nas funções**: decisão deliberada de adiar seed strategy para Fase 10. Stub força conversa (test-only endpoint vs fixtures vs DB direto) em vez de improvisar.
- **`flakyRoute()` simples**: não suporta delay variável ou jitter. Fase 10+ pode estender se padrão aparecer.
- **`hasAuthState()` lido em module load**: `useAuthRole()` decide skip no carregamento do spec, não por teste. Se storage state aparecer mid-run, primeiro spec já foi descartado. Aceitamos — auth setup roda antes via `dependencies`.
- **Sem `BasePage` populated**: só abstract + `goto/waitForLoad`. Pages concretas vêm com specs em Fase 10 — antecipar perde valor (não sabemos quais seletores vão importar até escrever os specs).
- **Mobile projects sem grep/filtro**: rodam por default. CI Fase 17 vai filtrar via matrix ou `--project chromium-desktop` para reduzir custo.
- **Smoke piloto só valida landing**: confirma que infra (config + fixtures + report) está sã, não que app funciona. Fase 10 expande smoke com 5 fluxos críticos (login admin, listar alunos, criar+cleanup, checkout 1 plano, health).
- **Engines warning Storybook persiste**: Node local 20.17 vs Storybook 10 (≥20.19). Playwright funciona em 20.17, então adicionar Playwright não piora; mas CI Node 22 continua sendo a fonte da verdade.

### Mudanças

#### Arquivos novos

- `frontend/playwright.config.ts` — 5 projects + setup, sharding-ready, expect/use config
- `frontend/tsconfig.e2e.json` — herda base + types `@playwright/test`
- `frontend/e2e/auth.setup.ts` — gera `.auth/{admin,aluno,treinador}.json` via `/api/auth`
- `frontend/e2e/fixtures/test-base.ts` — `test` custom com fixtures network + consoleErrors + `useAuthRole`
- `frontend/e2e/fixtures/auth.ts` — `authStatePath`, `hasAuthState`, type `AuthRole`
- `frontend/e2e/fixtures/network.ts` — `applyNetwork`, `goOffline`, `goOnline`, `flakyRoute`, presets
- `frontend/e2e/pages/BasePage.ts` — abstract POM base
- `frontend/e2e/utils/stripe.ts` — `STRIPE_TEST_CARDS` + expiry/cvc/zip
- `frontend/e2e/utils/memory.ts` — `measureHeap`, `forceGC`
- `frontend/e2e/utils/assert-csp.ts` — `parseCsp`, `assertCspDirective`, `assertCspHasDirective`
- `frontend/e2e/utils/seed.ts` — stubs com erro
- `frontend/e2e/specs/smoke/health.spec.ts` — piloto `@smoke`
- `frontend/e2e/README.md` — documentação

#### Arquivos atualizados

- `frontend/package.json` — `@playwright/test`, `eslint-plugin-playwright`; scripts e2e/e2e:ui/e2e:debug/e2e:smoke/e2e:security/e2e:lgpd/e2e:update-snapshots/e2e:install
- `frontend/eslint.config.mjs` — `plugin-playwright` em `e2e/**` + `playwright.config.ts`; rules-of-hooks off (Playwright `use(fixture)` triggera FP); no-console off; detect-non-literal-fs-filename off
- `frontend/.gitignore` — `/playwright/.cache/` + `/e2e/.auth/`
- `.github/CODEOWNERS` — `/frontend/e2e/`, `/frontend/tsconfig.e2e.json`

### Métricas de sucesso

- ✅ `@playwright/test` + `eslint-plugin-playwright` instalados
- ✅ 5 projects definidos (3 desktop + 2 mobile) com `dependencies: ["setup"]`
- ✅ `auth.setup.ts` cria storage state por role; skip elegante sem env
- ✅ 4 fixtures custom (network, consoleErrors) + helper `useAuthRole`
- ✅ 4 utils (stripe, memory, csp, seed) com APIs canônicas
- ✅ Smoke piloto `@smoke` em `e2e/specs/smoke/health.spec.ts`
- ✅ `npm run validate`: tsc 0 erros + ESLint 0 erros (30 warnings baseline Fase 7) + 355 vitest verdes
- ✅ `e2e/README.md` documenta quick-start + env vars + estrutura
- ✅ CODEOWNERS cobre `e2e/`, `tsconfig.e2e.json`, `playwright.config.ts`

### Impacto futuro

- Fase 10 (specs E2E críticos): popula `critical/`, `security/`, `lgpd/`, `multi-tab/`, `network/`, `a11y/`, `visual/`, `smoke/` em cima da infra desta fase
- Fase 11 (a11y + visual + memory): `measureHeap`/`forceGC` já existem; `axe-playwright` integra via fixture adicional
- Fase 13 (security): ZAP DAST roda contra preview deploy; `assert-csp` valida headers em unit-level E2E
- Fase 17 (CI completo): matrix sharding (`--shard N/M` × `--project <browser>`); secrets `E2E_*_EMAIL/PASSWORD` no GH Actions; preview deploy URL via `E2E_BASE_URL`
- Renovate (Fase 7): grupo `playwright` já configurado; bumps de `@playwright/test` chegam agrupados

---

## Fase 10a — Specs E2E críticos (POMs + smoke + critical)

**Status**: concluída (branch `chore/harness-fase10a-e2e-critical`).

A Fase 10 original (specs E2E críticos, 4d) foi dividida em duas — mesma estratégia da Fase 5:

- **10a (esta fase)**: 8 POMs + 5 smoke specs + 8 critical specs
- **10b (próxima)**: security (4) + lgpd (3) + multi-tab (2) + network (3) + a11y (1) + visual

### Objetivo (10a)

Cobrir o **happy path crítico** do produto com Playwright: login, CRUD admin, treino aluno, checkout Stripe, inatividade, responsive table e excel download. Estabelecer **fail loud** como contrato de execução — sem credenciais/dados de teste configurados, specs falham com mensagem clara em vez de skip silencioso.

### Por que

#### Fail loud (vs skip-gate)

- Fase 9 deixou `auth.setup.ts` com `setup.skip` quando env vars ausentes — permitia CI passar sem rodar nada de E2E. Custo: especificações ficam silenciosamente desativadas; PR pode mergear sem ninguém perceber que E2E está cego.
- Fase 10a inverte: `expect(email).toBeTruthy()` no setup falha **explicitamente** se `E2E_*_EMAIL/PASSWORD` ausente. Storage state file ausente em `useAuthRole()` lança Error pedindo configuração.
- Trade-off: PR só mergeia quando Fase 17 (CI) tiver secrets configurados. Aceito — é o ponto. Specs não rodam em CI atual (`npm run e2e` não está em workflow); rodam manualmente até Fase 17.

#### POM separado por área (admin/aluno/comum)

- 8 POMs concretos: `LoginPage`, `CadastroAlunoPage`, `CheckoutPage`, `admin/AdminAlunosPage`, `admin/AdminAlunoDetailPage`, `admin/AdminTreinadoresPage`, `aluno/AlunoFichasPage`, `aluno/AssinaturaPage`.
- Estrutura `pages/admin/` + `pages/aluno/` reflete o agrupamento route do Next (`(admin)/`, `(aluno)/`). Facilita encontrar.
- Locators por `getByRole/getByLabel` (a11y-first) em vez de CSS selectors — resilientes a refactor de classes MUI.

#### 5 smoke specs

Smoke valida o **app sobe e responde**, não funcionalidade profunda:

1. `health.spec.ts` — landing 2xx + zero `console.error` (já existia Fase 9, mantido)
2. `login-admin.spec.ts` — admin loga via UI + redireciona
3. `listar-alunos.spec.ts` — admin abre `/admin/alunos` + table OU empty state visível
4. `criar-aluno.spec.ts` — API-only: cria aluno via `/api/auth/register/aluno` + cleanup
5. `checkout.spec.ts` — aluno abre `/aluno/assinatura` + status visível

Smoke = rápido (~30s), sempre rodado pós-deploy (Fase 17 wire em CI).

#### 8 critical specs

Cobrem o **fluxo essencial** que sustenta o produto:

1. `auth.spec.ts` — login OK / login KO / logout (NÃO usa storageState — testa o login do zero)
2. `admin-aluno-crud.spec.ts` — seed via API + lista UI + filtro nome + filtro status + cleanup
3. `admin-treinador-crud.spec.ts` — lista + filtros (write actions adiadas — destrutivo em homolog real)
4. `aluno-treino.spec.ts` — lista fichas + abrir detalhe (skip se aluno sem fichas)
5. `checkout-stripe.spec.ts` — 3 cenários: success / decline / 3DS challenge (cartões test mode)
6. `inactivity.spec.ts` — `Date.now` stub viaja 5min / 20min e valida warn + logout
7. `responsive-table.spec.ts` — desktop renderiza `<table>`, mobile renderiza cards
8. `excel-download.spec.ts` — captura download + valida magic bytes XLSX (PK header zip)

#### Seed/cleanup real

- `e2e/utils/seed.ts` (Fase 9 era stub com erro) agora tem implementação real:
  - `seedAluno()`: POST `/api/auth/register/aluno` com `treinadorId` + `pacoteId` reais (env vars)
  - `findAlunoByEmail()`: GET admin/alunos lista paginada, busca exato
  - `cleanupAluno()` / `cleanupAlunoByEmail()`: DELETE admin
  - `makeTestEmail(prefix)`: sufixo timestamp evita colisão entre runs (`smoke-aluno+1716423456789@e2e.test`)
- Requer env vars adicionais: `E2E_TREINADOR_ID` + `E2E_PACOTE_ID` (treinador ativo + um pacote dele).

#### Inactivity via Date.now stub

- Hook real espera 20min sem atividade. Inviável em E2E.
- Spec faz `page.evaluate(() => { Date.now = () => start + 20*60*1000 + 1000 })` — viaja no tempo só pra `Date.now()`. `useInactivity` continua usando `setInterval` real (CHECK_MS=20s); espera o próximo tick disparar `onTimeout`.
- Trade-off: spec demora ~20-25s por causa do interval. Ainda inviável seria 20min real.

#### Responsive table

- Componente usa `useMediaQuery(theme.breakpoints.down("md"))` para alternar entre `<table>` (desktop) e cards (mobile).
- Spec usa `page.setViewportSize({ width: 1280, ... })` vs `{ width: 390, ... }` (iPhone) — força os dois modos no mesmo Chromium project sem precisar de mobile project separado.

#### Excel magic bytes (em vez de parsing)

- Validar XLSX completo exigiria reimportar exceljs no spec. Caro e duplicado.
- XLSX é ZIP por baixo — magic bytes `PK\x03\x04` (50 4B 03 04). Verificar primeiros 4 bytes é suficiente pra confirmar "é um zip válido". Conteúdo é responsabilidade do unit test (`src/lib/utils/excel.test.ts`).

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **5 smoke + 8 critical** | 13 specs cobrindo todo happy path admin/aluno |
| **8 POMs reutilizáveis** | Fase 10b ganha base; locators a11y-first sobrevivem refactor MUI |
| **Seed real** | `seedAluno/cleanupAluno` usam API real do backend; tests determinísticos |
| **Fail loud** | Setup explode se env var ausente; ninguém merge E2E "silencioso" |
| **Stripe 3 cenários** | Success / decline / 3DS — cobre os 3 estados que produção precisa |
| **Inactivity sem esperar 20min** | Date.now stub viaja no tempo; spec roda em 25s |
| **Excel sem dep** | Magic bytes em vez de parsing; unit test cobre conteúdo |
| **Auth flow completo** | Login OK + KO + logout em um único spec sem depender de storageState |

### Trade-offs aceitos

- **Specs assumem dados existem no homolog**: aluno com fichas, assinatura com pagamento pendente, treinadores ativos. Onde dados podem faltar, spec usa `test.skip()` interno (warn lint `playwright/no-skipped-test`, aceito).
- **`admin-treinador-crud` não testa write actions**: aprovar/reprovar/inativar/excluir são destrutivos em homolog compartilhado. Cobrimos só list+filtros. Fase 10b ou follow-up pode adicionar com seed dedicado.
- **`checkout-stripe` exige pagamento pendente seedado**: spec faz `beforeEach` checando botão "Pagar agora". Sem seed do backend (pagamento pendente Cartão com clientSecret), specs skipam. Aceito — Fase 10b ou backend seed.
- **`webhook stripe-cli` adiado**: validação fim-a-fim (pagamento → webhook → status na UI) precisa stripe-cli configurado em CI. Adiado para follow-up (Fase 13/17).
- **Lint warnings `no-conditional-in-test`, `no-skipped-test`, `prefer-to-have-count`**: aceitos no contexto Fase 10a — `test.skip()` interno é o melhor que dá com homolog compartilhado; conditional `if (count === 0)` é necessário para distinguir "ainda não criado" de "regressão".
- **POMs minimalistas**: cada POM expõe apenas locators usados por specs desta fase. Fase 10b expande conforme novos specs precisarem.
- **Selectors Stripe via `frameLocator`**: API oficial Playwright para iframes. Layout do PaymentElement pode variar entre versões — selector `iframe[name^='__privateStripeFrame']` é a convenção 2025; revisar se Stripe mudar.
- **`/api/backend/admin/alunos` no seed util**: usa proxy do Next em vez de chamar backend direto. Correto pois passa pelo middleware de Bearer; mantém comportamento idêntico ao UI admin.

### Mudanças

#### Arquivos novos

POMs:
- `frontend/e2e/pages/LoginPage.ts`
- `frontend/e2e/pages/CadastroAlunoPage.ts`
- `frontend/e2e/pages/CheckoutPage.ts`
- `frontend/e2e/pages/admin/AdminAlunosPage.ts`
- `frontend/e2e/pages/admin/AdminAlunoDetailPage.ts`
- `frontend/e2e/pages/admin/AdminTreinadoresPage.ts`
- `frontend/e2e/pages/aluno/AlunoTreinoPage.ts` (exporta `AlunoFichasPage`)
- `frontend/e2e/pages/aluno/AssinaturaPage.ts`

Smoke specs (5):
- `frontend/e2e/specs/smoke/login-admin.spec.ts`
- `frontend/e2e/specs/smoke/listar-alunos.spec.ts`
- `frontend/e2e/specs/smoke/criar-aluno.spec.ts`
- `frontend/e2e/specs/smoke/checkout.spec.ts`

Critical specs (8):
- `frontend/e2e/specs/critical/auth.spec.ts`
- `frontend/e2e/specs/critical/admin-aluno-crud.spec.ts`
- `frontend/e2e/specs/critical/admin-treinador-crud.spec.ts`
- `frontend/e2e/specs/critical/aluno-treino.spec.ts`
- `frontend/e2e/specs/critical/checkout-stripe.spec.ts`
- `frontend/e2e/specs/critical/inactivity.spec.ts`
- `frontend/e2e/specs/critical/responsive-table.spec.ts`
- `frontend/e2e/specs/critical/excel-download.spec.ts`

#### Arquivos atualizados

- `frontend/e2e/auth.setup.ts`: skip → `expect.toBeTruthy()` (fail loud)
- `frontend/e2e/fixtures/test-base.ts`: `useAuthRole` lança Error em vez de skip
- `frontend/e2e/fixtures/auth.ts`: comentário ajustado (fail loud)
- `frontend/e2e/utils/seed.ts`: stubs com erro → implementações reais via backend
- `frontend/e2e/specs/smoke/health.spec.ts`: comentário ajustado pra "Smoke 1/5"

### Métricas de sucesso

- ✅ 8 POMs criados, type-safe end-to-end
- ✅ 5 smoke specs em `e2e/specs/smoke/`
- ✅ 8 critical specs em `e2e/specs/critical/`
- ✅ `auth.setup.ts` fail loud (expect-based)
- ✅ `seed.ts` com 5 funções reais (seedAluno, findAlunoByEmail, cleanupAluno, cleanupAlunoByEmail, makeTestEmail)
- ✅ `npm run validate`: tsc 0 erros + ESLint 0 erros + 355 vitest verdes (zero regressão)
- ✅ Lint warnings novos categorizados (Playwright plugin FPs)

### Env vars necessárias para rodar

| Var | Função |
|-----|--------|
| `E2E_BASE_URL` | Onde apontar (`http://localhost:3000` default) |
| `E2E_ADMIN_EMAIL` + `E2E_ADMIN_PASSWORD` | Admin login + storage state |
| `E2E_ALUNO_EMAIL` + `E2E_ALUNO_PASSWORD` | Aluno login + storage state |
| `E2E_TREINADOR_EMAIL` + `E2E_TREINADOR_PASSWORD` | Treinador login + storage state |
| `E2E_TREINADOR_ID` | UUID de treinador ativo (seed de alunos) |
| `E2E_PACOTE_ID` | UUID de pacote do treinador (seed de alunos) |
| `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` | `pk_test_*` para checkout |

### Impacto futuro

- Fase 10b: security/lgpd/multi-tab/network/a11y/visual reutilizam POMs e fixtures Fase 10a
- Fase 11 (a11y dedicada): expande `axe-playwright` em cada POM
- Fase 13 (security): `assert-csp` (Fase 9) + cookies/CSRF/rate-limit specs Fase 10b
- Fase 14 (mutation): Stryker pode rodar critical specs como "integration" (lento mas alto sinal)
- Fase 17 (CI): job E2E com matrix sharding; secrets configurados; rodar smoke pós-deploy

---

## Fase 10b — Security + LGPD + multi-tab + network + a11y + visual

**Status**: concluída (branch `chore/harness-fase10b-e2e-security-lgpd`).

Segunda metade da Fase 10 original (após 10a). Cobre 14 specs em 6 categorias.

### Objetivo

Completar a cobertura E2E não-happy-path: segurança (CSP, cookies, CSRF, rate limit), LGPD (skeletons fail-loud para features ausentes), comportamento multi-aba, condições de rede degradadas, acessibilidade WCAG 2.1 AA e snapshots visuais.

### Por que

#### Security (4 specs)

- `csp-headers.spec.ts`: CSP é configurado em `next.config.ts` (`buildCsp()`). Sem teste E2E, mudanças no CSP passam silenciosamente. Spec valida 8 diretivas críticas + 5 headers complementares (X-Frame-Options, HSTS, Referrer-Policy, etc).
- `cookies-security.spec.ts`: cookies `token` + `session_guard` setados em `/api/auth/route.ts` com HttpOnly + SameSite=Strict + Secure(prod). Spec inspeciona via `context.cookies()` após login real — pega regressão se alguém remover flag.
- `csrf.spec.ts`: defesa primária é SameSite=Strict + Bearer no proxy `/api/backend/`. Spec valida (a) request cross-origin sem cookie retorna 401; (b) cookie repassado no header não dá acesso (proxy não confia em cookie do cliente).
- `rate-limit.spec.ts`: `checkRateLimit` em `src/lib/rateLimit.ts` permite 10 req/min/IP. Spec hammer-eia `/api/auth` com 12 attempts e espera transição 401 → 429.

#### LGPD (3 skeletons fail-loud)

Features ausentes no app:
- Banner de consentimento de cookies — não implementado
- `/api/perfil/excluir` (direito ao esquecimento) — não implementado
- `/api/perfil/exportar` (portabilidade) — não implementado

Per estratégia "fail loud" autorizada, specs ficam no repo como **skeletons que falham quando rodados**. Cada um tem comentário no topo descrevendo:
- Status atual (ausente)
- Onde implementar quando feature existir
- Que cenários expandir (banner aparece, persiste escolha, etc)

Quando spec falhar em CI (Fase 17), há 2 caminhos:
1. Implementar feature (banner / endpoints) → spec deve passar
2. Decisão consciente de não cobrir → remover spec com PR explicando

Trade-off: PR Fase 10b mergeia com specs LGPD que falhariam se E2E rodasse. Aceito — E2E não está em CI atual; quando entrar (Fase 17), o build vai pedir resolução.

#### Multi-tab (2 specs)

- `logout-cross-tab.spec.ts`: cria 2 `pages` no mesmo `BrowserContext` (cookies compartilhados, simulando 2 abas). Logout em pageA via `request.post("/api/auth/logout")` deve invalidar pageB — próxima navegação redireciona pra `/login` via middleware.
- `session-sync.spec.ts`: contexto com storage state admin → 2 pages → ambas acessam `/admin` sem redirect.

Multi-tab é diferente de multi-context: Playwright `BrowserContext` é isolado (cada contexto = nova "máquina"); pages no mesmo contexto compartilham cookies/storage — exatamente como abas reais do mesmo browser.

#### Network (3 specs)

Reusa fixtures Fase 9 (`network.slow3G/offline/flaky`):
- `slow3G.spec.ts`: navega `/admin/alunos` sob throttle CDP (~500Kbps + 400ms latency). Timeout 60s. Skip se browser ≠ chromium (CDP-only).
- `offline.spec.ts`: navega online → setOffline(true) → tenta nova rota → restaura conectividade.
- `retry.spec.ts`: `flakyRoute` falha 1ª request com 503; spec valida que UI mostra banner de erro (apiClient axios NÃO retenta automaticamente — decisão do projeto). Reload manual recupera. Regressão-proof: se alguém adicionar retry sem teste, UI ignoraria o erro inicial e este spec detectaria.

#### A11y all-pages-axe (1 spec, ~14 cenários)

- Instalado `@axe-core/playwright@^4`.
- Helper `e2e/utils/axe.ts` encapsula `AxeBuilder` com tags WCAG 2.1 AA + `disableRules(["color-contrast"])`.
- Spec varre 4 grupos via `describe`-scoped `test.use({ storageState })`:
  - Públicas (4 rotas): `/`, `/login`, `/cadastro/aluno`, `/cadastro/treinador`
  - Admin (3 rotas): `/admin`, `/admin/alunos`, `/admin/treinadores`
  - Aluno (3 rotas): `/aluno`, `/aluno/fichas`, `/aluno/assinatura`
  - Treinador (2 rotas): `/treinador`, `/treinador/alunos`
- Assertion: `results.violations === []` com JSON pretty-printed em caso de falha (debug rápido).
- `color-contrast` desabilitado: tema MUI tem casos de baixo contraste em chips/text.secondary que viraram dívida. Cobrir em fase a11y dedicada (Fase 11 do plano original) ou agora como follow-up.

#### Visual (1 spec piloto)

- `visual/login.spec.ts`: snapshot `toHaveScreenshot("login.png", { fullPage: true })`.
- Aguarda `document.fonts.ready` antes do snapshot — reduz flake por jitter de carregamento de fontes.
- Threshold via `playwright.config.ts`: `maxDiffPixelRatio: 0.01`, `threshold: 0.2`.
- Baselines **Linux-only** (geradas em Fase 17 CI). Local não gera baseline (regra do plano §12).
- Apenas 1 página (login) como piloto — Fase futura expande para admin/aluno/checkout + light/dark.

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **Security cobertura completa** | CSP + cookies + CSRF + rate limit em E2E real |
| **LGPD documentado como dívida** | Skeletons fail-loud forçam conversa quando CI ligar |
| **Multi-tab proof** | 2 specs garantem que logout invalida todas abas |
| **Network real (CDP)** | slow3G/offline/flaky em Chromium via fixtures Fase 9 |
| **A11y automatizado** | 14 cenários axe cobrindo todas roles + públicas |
| **Visual baseline ready** | Spec pronto; CI Fase 17 gera baseline Linux |
| **Reutiliza POMs + fixtures Fase 10a** | Zero duplicação |
| **355 vitest verdes mantidos** | Sem regressão |

### Trade-offs aceitos

- **LGPD specs falham quando run**: features ausentes. Decisão consciente — alternativa seria remover specs e esquecer LGPD existe. Skeleton no repo é gatilho permanente.
- **A11y `color-contrast` desabilitado**: tema MUI legado tem dívida de contraste. Cobrir em fase a11y dedicada (futura) ou hardening separado. Não bloqueia esta fase.
- **Visual só `/login`**: piloto. Expansão (admin/aluno/checkout, light/dark) em fase futura.
- **Retry spec valida AUSÊNCIA de retry**: o projeto opta por mostrar erro + reload manual em vez de retry automático. Spec garante que essa decisão se mantenha (regressão-proof).
- **CSRF token não implementado**: SameSite=Strict + Bearer no proxy backend são suficientes. Spec documenta a defesa atual e valida cenários adversarios.
- **`a11y/all-pages-axe` em 1 arquivo**: 4 describes + storage state diferente por bloco. Alternativa seria 4 arquivos separados; aceitamos 1 arquivo por simplicidade (plano §7 prevê 1 spec).
- **Lint warnings novos (~8)**: Playwright `no-conditional-expect`, `no-conditional-in-test`, `no-element-handle`, etc. Aceitos no contexto de specs que dependem de estado de homolog compartilhado.

### Mudanças

#### Deps

- `@axe-core/playwright@^4` (devDependency)

#### Arquivos novos

Security (4):
- `e2e/specs/security/csp-headers.spec.ts`
- `e2e/specs/security/cookies-security.spec.ts`
- `e2e/specs/security/csrf.spec.ts`
- `e2e/specs/security/rate-limit.spec.ts`

LGPD (3 skeletons):
- `e2e/specs/lgpd/consent-cookies.spec.ts`
- `e2e/specs/lgpd/delete-account.spec.ts`
- `e2e/specs/lgpd/export-data.spec.ts`

Multi-tab (2):
- `e2e/specs/multi-tab/logout-cross-tab.spec.ts`
- `e2e/specs/multi-tab/session-sync.spec.ts`

Network (3):
- `e2e/specs/network/slow3G.spec.ts`
- `e2e/specs/network/offline.spec.ts`
- `e2e/specs/network/retry.spec.ts`

A11y (1):
- `e2e/utils/axe.ts` (helper `runAxe`)
- `e2e/specs/a11y/all-pages-axe.spec.ts`

Visual (1):
- `e2e/specs/visual/login.spec.ts`

#### Arquivos atualizados

- `e2e/README.md`: estrutura specs com todas categorias Fase 10b.
- `frontend/package.json`: `@axe-core/playwright` em devDeps.

### Métricas de sucesso

- ✅ 14 specs novos em 6 categorias (security 4, lgpd 3, multi-tab 2, network 3, a11y 1, visual 1)
- ✅ `@axe-core/playwright` integrado via helper `runAxe`
- ✅ Reusa fixtures Fase 9 + POMs Fase 10a (zero duplicação)
- ✅ `npm run validate`: tsc 0 erros + ESLint 0 erros (49 warnings — baseline + Playwright FPs novos) + 355 vitest verdes
- ✅ Fail-loud LGPD documentado no spec + rationale
- ✅ `e2e/README.md` atualizado com novas categorias

### Impacto futuro

- Fase 11 (a11y dedicada): habilitar `color-contrast` + cobertura adicional axe; integrar Storybook test-runner com axe
- Fase 13 (security): ZAP DAST roda contra preview deploy complementando estes specs
- Fase 17 (CI): job E2E roda todas categorias; baseline visual gerada uma vez
- LGPD features (quando implementadas): specs já existem, basta passar
- Retry: se decisão mudar (adicionar retry automático), spec precisa atualizar primeiro

---

## Fase 11 — A11y vitest-axe + visual expandido + memory leak

**Status**: concluída (branch `chore/harness-fase11-a11y-visual-memory`).

### Objetivo

Fase 11 fecha o tripe de qualidade não-funcional: acessibilidade em nível de componente (vitest-axe), cobertura visual ampliada para todas roles, e detecção de memory leak via navegação repetida. Complementa Fase 10b (axe E2E + visual piloto) com profundidade no Vitest e amplitude no Playwright.

### Por que

#### A11y em duas camadas (vitest-axe + axe-playwright)

Fase 10b cobriu axe **no nível de página** (E2E completo). Fase 11 adiciona axe **no nível de componente** (vitest-axe + jsdom + render isolado):

- **Por que ambos**: violations comuns aparecem em componentes (chip sem aria-label, spinner sem nome acessível, alert sem role). Pegar no nível de componente é mais rápido (~100ms por test vs ~10s E2E) e mais granular — falha aponta o componente exato.
- **vitest-axe** (fork de jest-axe): `expect(container).toHaveNoViolations()`. Imports `vitest-axe/extend-expect` no setup integration.
- **Cobertura inicial**: 4 componentes piloto (mesmos do Storybook Fase 8): AlertBanner, StatusChip, LoadingSpinner, EmptyState. 12 testes cobrindo todas variações (severities, sizes, action button).

#### Bug real encontrado

vitest-axe **pegou bug** em LoadingSpinner já no primeiro run: `CircularProgress` sem aria-label viola `aria-progressbar-name`. Fix: prop `label?: string` default "Carregando" passada como `aria-label`. Hoje 1 componente, amanhã pode pegar dezenas — vale o investimento.

#### Visual snapshots por role

Fase 10b deixou piloto só de `/login`. Fase 11 expande para **8 rotas distribuídas em 4 specs**:

- `visual/public.spec.ts` (3 rotas): landing, cadastro/aluno, cadastro/treinador
- `visual/admin.spec.ts` (3 rotas): /admin, /admin/alunos, /admin/treinadores
- `visual/aluno.spec.ts` (2 rotas): /aluno, /aluno/assinatura
- `visual/treinador.spec.ts` (2 rotas): /treinador, /treinador/alunos

Cada spec usa `useAuthRole(test, role)` no escopo do arquivo. Cada snapshot espera `document.fonts.ready` antes do screenshot (anti-flake).

Baselines **Linux-only**. CI Fase 17 gera baselines pela primeira vez via `playwright test --update-snapshots`. Local não deve gerar — fontes/OS diferentes mascaram regressão real.

#### Memory leak via navegação repetida

Padrão clássico de leak detection:
1. Warmup (2 navegações), forceGC, medir baseline
2. Stress (10 navegações alternadas alunos ↔ treinadores)
3. forceGC, medir final
4. Assertion: `final / baseline < 1.5`

Threshold 1.5x conservador per plano §12 (evita false positive — heap legítimo cresce com cache, fonts, etc).

Requer:
- Chromium com `--js-flags=--expose-gc` (window.gc disponível)
- `performance.memory` API (Chromium-only)

Spec skipa em Firefox/WebKit com mensagem clara. Em CI Fase 17, job de memory leak roda só em projeto chromium.

Helpers `measureHeap()` + `forceGC()` já existem em `e2e/utils/memory.ts` desde Fase 9 — Fase 11 finalmente os usa.

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **A11y dois níveis** | vitest-axe rápido (componente) + axe-playwright amplo (página) |
| **Bug real pego** | LoadingSpinner aria-label adicionado por causa de vitest-axe |
| **12 a11y tests novos** | 4 componentes × 2-4 variações |
| **8 snapshots novos** | Cobertura visual completa por role |
| **Memory leak quantificado** | Threshold 1.5x conservador; assertion explícita |
| **Reusa utils Fase 9** | `measureHeap`/`forceGC` finalmente exercitados |
| **367 vitest verdes** | +12 a11y (era 355); zero regressão |
| **Lint zero erros** | 51 warnings (baseline + 2 novos memory/visual) |

### Trade-offs aceitos

- **vitest-axe 1.0.0-pre.5**: versão pre-release ainda. API estável (fork de jest-axe). Renovate pinned; revisar quando 1.0.0 estável sair.
- **Componentes piloto apenas (4)**: cobertura inicial. Cada nova adição de componente UI deve trazer `.a11y.test.tsx` ao lado (convenção). Storybook Fase 8 e a11y Fase 11 caminham juntos.
- **Visual snapshots sem light/dark**: app não tem dark mode atualmente. Quando implementar, expandir cada spec com `{ name, theme: "dark" }` variante.
- **Memory leak threshold 1.5x conservador**: pode mascarar leak gradual. Fase futura pode adicionar regressão tracking (delta entre runs) para detectar tendência. Por ora, 1.5x absoluto basta para leak grosseiro.
- **Visual specs admin/aluno/treinador dependem de seed estável**: dados dinâmicos (lista de alunos, status) variam entre runs em homolog. Aceitamos com threshold global `maxDiffPixelRatio: 0.01`. Se ficar flaky, mascarar regiões dinâmicas via `mask` option ou seed dedicado.
- **Memory leak só em Chromium**: Firefox/WebKit não expõem heap API. Aceitamos — memory leak é cross-browser na prática (V8 vs SpiderMonkey vs JavaScriptCore comportam-se similar para o tipo de leak que pegamos).
- **LoadingSpinner default `label="Carregando"`**: hardcoded pt-BR. Quando i18n entrar, expor via prop ou contexto. Por ora, aceita-se default em pt-BR (idioma único do app).

### Mudanças

#### Deps

- `vitest-axe@^1.0.0-pre.5`
- `axe-core@^4` (peer requirement)

#### Arquivos novos

A11y component tests (4):
- `src/components/ui/AlertBanner.a11y.test.tsx`
- `src/components/ui/StatusChip.a11y.test.tsx`
- `src/components/ui/LoadingSpinner.a11y.test.tsx`
- `src/components/ui/EmptyState.a11y.test.tsx`

Visual specs (4):
- `e2e/specs/visual/public.spec.ts`
- `e2e/specs/visual/admin.spec.ts`
- `e2e/specs/visual/aluno.spec.ts`
- `e2e/specs/visual/treinador.spec.ts`

Memory leak (1):
- `e2e/specs/memory/navigation-leak.spec.ts`

#### Arquivos atualizados

- `frontend/src/test/setup/integration.ts`: importa `vitest-axe/extend-expect`
- `frontend/src/components/ui/LoadingSpinner.tsx`: prop `label` + aria-label (fix a11y)
- `frontend/package.json`: deps vitest-axe + axe-core

### Métricas de sucesso

- ✅ `vitest-axe` integrado em integration setup
- ✅ 12 a11y tests novos passando (4 componentes piloto)
- ✅ Bug real detectado e corrigido (LoadingSpinner aria-label)
- ✅ 8 visual specs novos (3 públicos + 3 admin + 2 aluno + 2 treinador)
- ✅ 1 memory leak spec com threshold 1.5x
- ✅ `npm run validate`: tsc 0 erros + ESLint 0 erros + **367 vitest verdes** (era 355)

### Impacto futuro

- Convenção a partir desta fase: novo componente UI vem com `.a11y.test.tsx` ao lado
- Fase 17 (CI): job `visual` gera baselines Linux pela primeira vez; job `memory` roda chromium-only
- Fase futura (dark mode): visual specs ganham variante dark
- Fase futura (i18n): LoadingSpinner.label vira chave de tradução
- Lint hardening: violations a11y em componentes podem virar regra lint custom

---

## Fase 12 — Lighthouse CI + bundle-analyzer + linkinator

**Status**: concluída (branch `chore/harness-fase12-lighthouse-bundle`).

### Objetivo

Adicionar gates de performance sintética (Lighthouse CI), introspeção de bundle (next/bundle-analyzer) e validação de links quebrados (linkinator). Diferente das fases anteriores que cobriam correção, esta fase cobre **velocidade percebida + custo de payload**.

### Por que

#### Lighthouse CI

- Performance score, accessibility score, best-practices e Core Web Vitals (LCP, CLS, TBT) impactam diretamente conversão + SEO. Sem gate automatizado, regressão entra produção silenciosa.
- LHCI roda 3 vezes por URL e usa mediana (anti-flake). Compara contra budgets configurados em `lighthouserc.json`.
- 4 URLs cobertas inicialmente: landing, login, cadastro/aluno, cadastro/treinador. Todas públicas (sem auth) — LHCI não autentica.
- Budgets (per plano §5):
  - performance ≥ 0.85
  - accessibility ≥ 0.95
  - best-practices ≥ 0.9
  - LCP ≤ 2500ms
  - CLS ≤ 0.1
  - TBT ≤ 300ms

#### Bundle analyzer

- `@next/bundle-analyzer` integra com `next build` quando `ANALYZE=true`. Gera 2 reports HTML (server + client) com treemap visual mostrando o que cada chunk inclui.
- Detectar bloat (lib pesada desnecessária, duplicação de versões, imports não tree-shaken) é manual hoje. Bundle analyzer materializa o problema.
- Renovate (Fase 7) sugere bumps de MUI/Stripe etc — analyzer mostra impacto real no payload antes de aceitar bump.

#### Linkinator

- Páginas com links `<a href>` para rotas internas e externas. Link quebrado interno = bug; externo = link rot.
- `linkinator http://localhost:3000 --recurse --skip "^https?://(?!localhost)"` varre apenas internos (pular externos por padrão — evita 429 contra GitHub/Twitter/etc).
- Roda em CI (Fase 17) contra build estático ou dev server.

#### Scripts antecipados (Fase 17 pode usar)

- `analyze`: `cross-env ANALYZE=true next build`
- `lhci`: `lhci autorun` (collect + assert + upload)
- `lhci:collect`, `lhci:assert`: estágios isolados para CI debugging
- `links`: linkinator contra localhost

#### Por que `cross-env`

- Windows não interpreta `ANALYZE=true command`. `cross-env` é o padrão npm para variáveis de ambiente cross-platform. Dependência mínima (zero runtime cost).

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **Gates de performance** | LHCI bloqueia PR se LCP > 2.5s, CLS > 0.1, TBT > 300ms |
| **Performance budget explícito** | `lighthouserc.json` versionado; histórico de mudanças audível |
| **Bundle introspection on-demand** | `npm run analyze` antes de merge de bump de deps |
| **Link rot detection** | linkinator pega href quebrado interno antes de produção |
| **Anti-flake LHCI** | 3 runs, mediana — reduz falsos positivos |
| **Upload temporário grátis** | LHCI usa `temporary-public-storage` — comparação cross-PR sem servidor próprio |
| **Bundle analyzer transparente** | `ANALYZE=true next build` ativa wrapper; sem afetar build padrão |

### Trade-offs aceitos

- **LHCI requer Chrome instalado**: CI Fase 17 instalará via Playwright deps. Local: dev precisa Chrome no PATH ou usa Docker.
- **`startServerCommand` em LHCI**: precisa `npm run start` funcionar (build prévio necessário). CI Fase 17 builda antes.
- **Budgets conservadores em SEO (warn, não error)**: app não é landing pública intensa; SEO menos crítico que performance pura. Pode hardener depois.
- **`uses-text-compression` off**: dev local sem gzip configurado. Em produção (nginx + brotli/gzip), passa. CI Fase 17 com proxy de prod pode reativar.
- **`csp-xss` off**: heurística do Lighthouse marca CSP com `'unsafe-inline'` como warn. Aplicação usa `'unsafe-inline'` necessariamente (Next.js hydration sem nonce + Emotion injeta estilos inline). Decisão arquitetural; CSP é defesa-em-profundidade, não única.
- **`uses-rel-preconnect` off**: heurística sugere preconnect para domínios externos (Stripe). Premature optimization neste estágio; reativar quando otimizar checkout.
- **linkinator pula externos por padrão**: domínios externos (GitHub, docs.stripe.com) geram 429/timeouts em CI. Externos validados manualmente quando necessário.
- **`@lhci/cli` 259 packages**: peso transitivo (puppeteer, axe, etc). Aceitamos — só devDep, não impacta bundle.
- **Bundle analyzer só ativa com `ANALYZE=true`**: build padrão não muda. Trade-off zero — só liga sob demanda.

### Mudanças

#### Deps

- `@lhci/cli@^0.15.1`
- `@next/bundle-analyzer@^16.2.6`
- `linkinator@^6.3.0`
- `cross-env@^7.0.3` (variáveis de ambiente cross-platform)

#### Arquivos novos

- `frontend/lighthouserc.json` — config LHCI completa (4 URLs, budgets, anti-flake)

#### Arquivos atualizados

- `frontend/next.config.ts` — `withBundleAnalyzer(nextConfig)` quando `ANALYZE=true`
- `frontend/package.json` — scripts `analyze`, `lhci`, `lhci:collect`, `lhci:assert`, `links`
- `frontend/.gitignore` — `/.lighthouseci/`
- `.github/CODEOWNERS` — `lighthouserc.json`

### Métricas de sucesso

- ✅ Deps instaladas (4 novas)
- ✅ `lighthouserc.json` versionado com 4 URLs + budgets
- ✅ `next.config.ts` wrapped `withBundleAnalyzer`
- ✅ 5 scripts npm novos (analyze, lhci, lhci:collect, lhci:assert, links)
- ✅ `npm run validate`: tsc 0 erros + ESLint 0 erros + 367 vitest verdes (zero regressão)

### Impacto futuro

- Fase 17 (CI completo): job `lighthouse` roda LHCI contra preview deploy (Vercel/CF Pages)
- Fase 17: job `links` roda linkinator após deploy
- Bundle analyzer pode virar parte do PR checks via comentário automático (delta bundle size vs main) — Fase 17 ou follow-up
- LHCI dashboard via `target: "lhci"` (servidor próprio) ou `target: "temporary-public-storage"` (free, retenção 7 dias) — atual usa temporary
- Renovate + bundle analyzer: PR de bump pode incluir delta de bundle automatizado

---

## Fase 13 — Security gates (audit + osv + gitleaks + ZAP + SBOM + license + CodeQL)

**Status**: concluída (branch `chore/harness-fase13-security-gates`).

### Objetivo

Adicionar gates de segurança defesa-em-profundidade: supply chain (npm audit + OSV + license + SBOM), SAST (CodeQL + gitleaks) e DAST (OWASP ZAP). Configs versionadas; workflows GH Actions vêm em Fase 17.

### Por que

#### npm audit (production-only)

- `npm audit` default inclui devDeps. Devs trazem vulns transitivos (e.g., `nyc`, `jest-junit`) que nunca rodam em produção mas inflam o relatório e causam ruído.
- Script `audit`: `npm audit --omit=dev --audit-level=high`. Falha CI se houver **high** ou **critical** em prod.
- Script auxiliar `audit:dev`: cobre devDeps quando reviewer quiser inspecionar (manual, não CI).
- Threshold `high` (não `moderate`): moderate em prod é dívida documentada (ex: `uuid` interno do exceljs); high/critical bloqueia merge.

#### OSV Scanner

- npm audit usa GitHub Advisory Database (GHSA). OSV.dev é meta-database (GHSA + RustSec + PyPA + Go + ...) — cobertura maior, especialmente para deps transitivas que GitHub não indexou.
- Config `osv-scanner.toml` no root. CI Fase 17 roda `osv-scanner --config osv-scanner.toml --recursive .`.
- Hoje vazio em `IgnoredVulns` — qualquer ignore deve ter `reason` + `ignoreUntil`.

#### Gitleaks (secrets detection)

- Detecta secrets (API keys, JWTs, AWS creds, etc) commitados acidentalmente. Pre-commit defesa primária; CI defesa secundária.
- Config `.gitleaks.toml` extende defaults + allowlist do projeto:
  - Stripe test mode keys (`pk_test_*`, `sk_test_*`) — públicas por design
  - JWT placeholders em testes
  - UUIDs zero (`00000000-...`) em fixtures
  - Paths de teste e arquivos gerados (openapi.json, msw/types.ts)

#### License checker

- Projeto comercial → licenses copyleft (GPL-3.0, AGPL-3.0, LGPL-3.0, CDDL, EPL) podem contaminar código fechado.
- Script `license`: `license-checker --production --failOn "GPL-3.0;AGPL-3.0;LGPL-3.0;CDDL-1.0;EPL-1.0;EPL-2.0"`.
- Smoke test local mostrou: 204 MIT, 27 ISC, 9 Apache-2.0, 7 BSD-3-Clause. **Zero copyleft proibido**. Passa.
- `--production` exclui devDeps (devs podem rodar GPL/AGPL livremente).

#### CycloneDX SBOM

- SBOM = Software Bill of Materials. Padrão emergente (NTIA / EU CRA / executive orders) — eventualmente obrigatório para software comercial.
- `cyclonedx-npm` gera `sbom.cdx.json` (CycloneDX JSON 1.5). Inclui versões + licenças + hashes + dependency graph.
- Artefato CI Fase 17 anexado em release. Permite auditoria reversa de "que versão de X estava no release Y".

#### CodeQL (SAST)

- GitHub-nativo, free para repos públicos / Advanced Security para privados.
- `paths` aponta para `frontend/src` + 4 projects .NET. `paths-ignore` exclui tests, generated, build artifacts.
- `queries: security-extended + security-and-quality` — catch maior que default.
- Workflow `.github/workflows/codeql.yml` vem Fase 17.

#### OWASP ZAP (DAST)

- DAST = roda contra app rodando, descobre vulns que SAST não pega (XSS reflexivo, IDOR, header missing, redirects abertos).
- Config `zap.yaml` usa Automation Framework do ZAP — declarativo, versionável.
- Context: `https://homologacao.forzion.tech/`. Spider 5min + passive scan. Report HTML.
- `excludePaths`: `/api/auth` (rate limit), assets binários.
- `alertFilter`: marca falso-positivos conhecidos (XFO já DENY via headers, CSP `unsafe-inline` arquitetural).
- Roda via Docker em CI Fase 17 contra preview deploy ou homolog estável.

### Vantagens

| Vantagem | Concretude |
|----------|------------|
| **Supply chain double-source** | npm audit (GHSA) + OSV (multi-source) cobrem mais vulns que cada um sozinho |
| **License copyleft bloqueado** | gate explícito em GPL/AGPL/LGPL/CDDL/EPL |
| **SBOM versionado** | `sbom.cdx.json` gerado on-demand; CI publica como artefato |
| **Gitleaks com allowlist tipada** | Stripe test keys + JWT placeholders documentados explicitamente |
| **CodeQL pronto** | config aponta paths corretos; workflow vem Fase 17 |
| **ZAP automation declarativa** | `zap.yaml` versionado; alertFilter documenta FPs |
| **CI-friendly threshold** | `npm audit --omit=dev --audit-level=high` falha apenas em prod high/critical |
| **Zero regressão** | 367 vitest verdes mantidos |

### Trade-offs aceitos

- **`npm audit --omit=dev`**: devDeps high não bloqueiam merge. Fase futura: tratar separadamente (Renovate auto-merge patches).
- **2 moderate em prod** (uuid via exceljs): aceito por enquanto — `audit-level=high` não dispara. exceljs major bump requer migration; aguardando Renovate ou refactor.
- **27 vulns em devDeps**: heranças de cyclonedx-npm / lhci / linkinator transitivos. Não bloqueia (devDep, não shipped). `audit:dev` permite inspeção manual.
- **OSV ignored list vazio**: zero exceções no início. Se OSV reportar FP, adicionar com `reason + ignoreUntil` documentado.
- **Gitleaks allowlist regex específica**: pode mascarar real secret se padrão coincidir. Aceitamos — Stripe test keys e JWT placeholders são pattern-único.
- **ZAP só contra homolog** (não preview deploy ainda): preview deploy infra (Vercel/CF Pages) entra Fase 17. Por ora, homolog `https://homologacao.forzion.tech/` é o alvo.
- **CodeQL paths-ignore inclui tests**: deliberadamente — tests podem ter padrões "perigosos" intencionais (mock secrets, eval simulado, etc) que disparam FP.
- **SBOM gerado on-demand, não em build**: gerar SBOM em todo build adiciona ~30s. CI Fase 17 gera apenas em release/main.
- **License-checker em prod-only**: devDep GPL é OK (compilador, formatter, etc não shipped). Aceito.

### Mudanças

#### Deps

- `@cyclonedx/cyclonedx-npm@^2`
- `license-checker@^25`

#### Arquivos novos (root + frontend)

- `.gitleaks.toml` — config secrets detection + allowlist
- `osv-scanner.toml` — config OSV scanner
- `zap.yaml` — config ZAP automation framework
- `.github/codeql/codeql-config.yml` — config CodeQL paths + queries

#### Arquivos atualizados

- `frontend/package.json` — scripts `audit`, `audit:dev`, `license`, `sbom`, `security:all`
- `frontend/.gitignore` — `/sbom.cdx.json`, `/zap-report/`
- `.github/CODEOWNERS` — `.gitleaks.toml`, `osv-scanner.toml`, `zap.yaml`, `.github/codeql/`

### Métricas de sucesso

- ✅ 2 deps novas instaladas (@cyclonedx/cyclonedx-npm + license-checker)
- ✅ 5 scripts npm novos (audit, audit:dev, license, sbom, security:all)
- ✅ 4 configs novas (gitleaks, osv, zap, codeql)
- ✅ `npm run license`: 0 copyleft proibido detectado (204 MIT + 27 ISC + 9 Apache-2.0 + 7 BSD)
- ✅ `npm run sbom`: gera `sbom.cdx.json` ~3.9MB CycloneDX JSON
- ✅ `npm run audit`: 2 moderate em prod (< threshold high) — passa
- ✅ `npm run validate`: tsc 0 + ESLint 0 + 367 vitest verdes
- ✅ Pre-commit hook passou

### Impacto futuro

- Fase 17 (CI completo): 6 workflows novos — `codeql.yml`, `gitleaks.yml`, `osv.yml`, `zap.yml`, `sbom.yml`, `license.yml`
- Renovate (Fase 7): auto-merge patches de devDeps reduz vuln count
- ZAP roda contra preview deploy (Vercel/CF Pages) quando Fase 17 configurar
- SBOM artefato em releases (NTIA / EU CRA compliance)
- CodeQL Pull Request alerts automáticos
- Eventual: SLSA provenance attestation pipeline (cross-cutting com Fase 17)

---

## Próximas fases

A serem adicionadas à medida que concluídas:

- Fase 14 — Mutation testing (Stryker, semanal)
- Fase 11 — A11y + visual + memory leak
- Fase 12 — Lighthouse CI + bundle + crawl
- Fase 13 — Security gates
- Fase 14 — Mutation testing
- Fase 15 — Contract testing Pact
- Fase 16 — Sentry + Web Vitals RUM
- Fase 17 — CI completo + PR preview deploys
- Fase 18 — Observability + flake tracking
