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

## Próximas fases

A serem adicionadas à medida que concluídas:

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
