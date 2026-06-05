# specification-tests — estratégia e disciplina de testes (forzion.tech)

DOC PARA AGENTES. Fonte de verdade de COMO testar, COMO o projeto impõe testes (enforcement) e a disciplina inegociável de execução. Formato denso, agent-oriented. Consultar antes de criar/alterar testes, mexer em gates de CI/hooks, thresholds de cobertura, mutation, contract, ou ao decidir "como/se testar X". Princípio-mestre: **regra sem enforcement não funciona** — toda regra aqui aponta o mecanismo que a faz valer.

## MANUTENÇÃO DESTE ARQUIVO
- Atualizar NA MESMA TAREFA de mudança em: frameworks de teste, tipos/isolamento, gates (hooks/CI), thresholds (cobertura/mutation), fixtures, categorias, baseline de contagem.
- Vive em `specs/` (versionado; NÃO `.specs/`). CI ignora `specs/**` (paths-ignore) → mudar spec não dispara pipeline.
- Estrutura de banco de teste → [specification-db]; fluxos cobertos → specs de domínio.

## 0. DETECÇÃO DATA-DRIVEN (não assumir — descobrir)
A infra de teste DEVE ser detectada da realidade do repo, NUNCA hardcoded. Cada projeto tem frameworks, tipos e isolamento próprios. Procedimento de detecção (rodar antes de planejar testes):
- **Backend**: `*.csproj`/`Directory.Build.props` (TFM, analyzers, `WarningsAsErrors`), `*.runsettings`, atributos `[Trait(...)]`/`[Collection(...)]` (categorias/isolamento), `stryker-config.json` (mutation), `forzion.tech.PactVerification/` (contract provider).
- **Frontend**: `package.json` (scripts `test*`/`e2e*`/`storybook*`), `vitest.config.mts` (projects + thresholds por glob), `playwright.config.ts` (projects/browsers), `.husky/` (hooks), `commitlint`.
- **Pipeline**: `.github/workflows/*` — cada job é um gate; ler `needs`/`gate` p/ saber o que é bloqueante.
- A SNAPSHOT abaixo (§3-§9) é o estado detectado em 2026-05-29. Se divergir do repo, o repo vence — re-detectar e atualizar este arquivo.

## 1. PRINCÍPIOS INEGOCIÁVEIS (cada um com enforcement)
1. **Enforcement obrigatório / verificação visível** — nenhuma regra de teste existe sem um gate que a imponha (hook pre-commit, job CI no `gate`, threshold). A verificação é VISÍVEL a partir desta spec: §7 lista comando+gate+bloqueante de cada regra. Sem gate ⇒ a regra não existe.
2. **Co-localização teste↔implementação** — o local do teste mapeia 1:1 o local da implementação, de forma determinística (achar o teste a partir do path do código sem busca).
   - Frontend: teste JUNTO do código (`*.test.ts(x)`, `__tests__/` irmão). E2E em `frontend/e2e/` (fora do bundle).
   - Backend: projeto espelho `forzion.tech.Tests/` com a MESMA árvore (`Domain/`, `Application/`, `Api/`, `Infrastructure/`) — idioma .NET; mirroring preserva a localidade estrutural. Enforcement: revisão estrutural (PR) — teste novo fora do espelho é rejeitado.
3. **Fase GREEN é intocável** — ao implementar p/ um teste existente, NUNCA modificar o teste pra passar. Se um teste falha, é PROIBIDO enfraquecer a asserção, marcar skip, comentar, ou deletar. Ação obrigatória: **PARAR e perguntar ao humano** o que fazer. Enforcement: disciplina + rastreio de contagem (§4) + revisão de diff (teste alterado junto de fix de green = red flag no PR).
4. **Rastrear CONTAGEM, não só pass/fail** — acompanhar total/passed/failed/skipped. Proibido deletar/skipar/`xfail` um teste que falha p/ "ficar verde": a contagem não pode cair sem justificativa humana. Gates de cobertura por piso (ex. Infra branch 35) também guardam contra a suíte de integração **parar de rodar em silêncio** (cobertura despencaria → falha = sinal desejado). Baseline §4.
5. **Dependência de compilação tem caminho de resolução** — é PROIBIDO adiar/pular teste com "não dá pra testar X por causa do módulo Y". Isso é sintoma de fronteira errada, não de teste impossível. Resolver via: interface/abstração na borda (Clean Arch: Application define interface, Infra implementa), test double/fake (ex. `FakeStripeService`), ou Testcontainers p/ infra real. Se realmente não há caminho, o design está errado → corrigir a fronteira, não pular o teste.
6. **Detecção data-driven, não hardcoded** — ver §0. Frameworks/tipos/isolamento variam por projeto; descobrir da realidade (csproj/package.json/configs/CI), nunca presumir.
7. **Nunca `--no-verify`** — bypass de hook é proibido por política (pre-commit comenta isso explicitamente). Hook falhou ⇒ corrigir a causa.
8. **Determinismo** — tempo via `TimeProvider` (testes injetam `FakeTimeProvider`); sem rede/relógio real; seeds fixas em property tests (`seedrandom`/`fast-check`). Flaky = bug, não tolerado.

## 2. STACK & ISOLAMENTO (detectado)
- **Backend** (.NET 8): xUnit. Projeto único `forzion.tech.Tests` multi-categoria + `forzion.tech.PactVerification` (provider Pact). Cobertura: Coverlet (msbuild). Mutation: Stryker.NET. Isolamento de integração: **Testcontainers (PostgreSQL)** — exige Docker.
- **Frontend** (Next 16/React 19): **Vitest 4** em 3 projects + **Playwright** (E2E) + **Storybook** (+ test-runner/a11y) + **Pact** (consumer, `vitest.pact.config.mts`) + **MSW** (mock de rede) + **fast-check** (property) + **axe** (a11y).

## 3. TIPOS DE TESTE (detectado)
### Backend (`forzion.tech.Tests/`)
- **Unit** (sem Docker): Domain (entidades/VOs/eventos), Application (handlers, validators, Result), Api (endpoints via mocks), Infra (handlers de notificação, webhook, decorators).
- **Integração/E2E** (Docker): `[Trait("Category","Integration")]` — `E2E/RealPipelineFixture` (WebApplicationFactory + Postgres efêmero + migrate+seed, handlers/infra REAIS, `FakeStripeService`), `Infrastructure/InfrastructureTestFixture` (repos contra PG real), `[Collection(E2ECollection)]`/`[Collection(InfrastructureTestCollection)]`.
- **Property** (cobertura de invariantes), **arch** (regras de camada), **snapshot**, **mutation** (Stryker).
### Frontend
- **vitest projects** (`vitest.config.mts`): `unit` (node — `src/lib/**`, `src/hooks/**`, `src/middleware`), `integration` (jsdom — `src/components/**`, `src/app/**/__tests__`, libs que tocam DOM, MSW), `api` (node — `src/app/api/**` route handlers).
- **property** (`*.property.test.ts`, fast-check — excluídos da cobertura), **contract** (Pact consumer, `npm run test:contract`), **E2E Playwright** (`e2e/specs/`: smoke/critical/security/lgpd/multi-tab/network/a11y/visual; 5 projects browser+mobile; `auth.setup` gera storage-state por papel; snapshots por-OS `*-{platform}.png`), **storybook** (+ a11y addon).

## 4. CONTAGEM / RASTREIO (baseline 2026-05-29)
- Backend: **1668** testes (unit + integração/Testcontainers) — verde. Frontend: **377** (vitest 3 projects) — verde. Playwright E2E: suíte completa só roda no CI Linux (creds `E2E_*` + browsers); validação local cobriu smoke+critical públicos/admin (público 4/4, admin 3/3 — [specification-local-ci-repro] §4); aluno/treinador bloqueados local. Alguns specs com seletor frágil — ver `.specs/qa`.
- Regra: a contagem NÃO regride sem decisão humana. Teste que falha **fica** (vermelho visível) até ser corrigido pelo código — não some. Cobertura por piso protege contra suíte parar de rodar (§7).

## 5. CO-LOCALIZAÇÃO (mapa)
- Frontend: `src/<area>/X.ts` → `src/<area>/X.test.ts` (ou `__tests__/X.test.tsx`). E2E: `frontend/e2e/specs/<categoria>/*.spec.ts` + page objects `e2e/pages/` + fixtures `e2e/fixtures/`. Factories: `src/test/factories/*`. Setup: `src/test/setup/{unit,integration,api}.ts`. MSW: `src/test/msw/`.
- Backend: `forzion.tech.<Camada>/.../X.cs` → `forzion.tech.Tests/<Camada>/.../XTests.cs` (espelho 1:1). Fixtures: `forzion.tech.Tests/E2E/`, `forzion.tech.Tests/Infrastructure/`.

## 6. ISOLAMENTO / FIXTURES & DUBLÊS (caminhos de resolução — princípio 5)
- **Stripe** → `FakeStripeService` (E2E) / mock (unit). Dummy keys bootam o app (ValidateOnStart). Resend/WhatsApp → `Null*` (sem envio real). Tempo → `FakeTimeProvider`.
- **DB real** → Testcontainers (RealPipelineFixture/InfrastructureTestFixture). Fronteira: Application define `I*Repository`/`I*Service`; Infra implementa. Teste de Application NUNCA depende de Infra concreta — usa a interface (por isso "não posso testar por causa do módulo X" não se aplica: a borda existe).
- **Rede (frontend)** → MSW (`admin.msw.test.ts` piloto + handlers). E2E → backend real (stack local) ou storage-state.
- Admin de seed p/ E2E: `admin@forzion.tech` (senha via `Seed:AdminPassword`).

## 7. ENFORCEMENT — GATES (comando · bloqueante · onde) ⟵ VERIFICAÇÃO VISÍVEL
### Local — hook pre-commit (`frontend/.husky/pre-commit`, por área staged; `--no-verify` PROIBIDO)
- Sequência canônica de passos (backend format→build→test; frontend typecheck→lint-staged→vitest) e commit-msg/commitlint: ver [specification-git] §PRE-COMMIT HOOK + §CONVENTIONAL COMMITS (não duplicar). Resumo: cada área staged roda seu gate, todos bloqueantes.

### CI — `.github/workflows/ci.yml` (job `gate` = required check, agrega os obrigatórios)
| Job | Gate | Bloqueante |
|-----|------|------------|
| `commitlint` (PR) | conventional commits | sim |
| `test-backend-unit` | build + `dotnet format --verify` + cobertura Coverlet: Domain branch 75 / line+method 85; Application branch 75 / line+method 85; Api line 85 / method 70 (filtro `Category!=Integration`) | sim |
| `test-backend-integration` | suíte COMPLETA (Testcontainers): global branch 50; Infrastructure branch 35 (exclui Migrations) | sim |
| `test-frontend` | `lint` + `tsc --noEmit` + `test:coverage` (vitest thresholds por glob, §8) | sim |
| `build-frontend` | `next build` + `storybook:build` | sim |
| `security` / `security-backend` | gitleaks, `npm audit --omit=dev >=high`, license-checker, OSV, SBOM; NuGet `--vulnerable` (gate manual), SBOM CycloneDX | sim |
### CI — workflows dedicados (separados do gate principal)
- `mutation.yml` — **Stryker.NET** matriz Domain+Application, `break 40` (high 80/low 60) — `stryker-config.json`.
- `contract.yml` / `pact-provider.yml` — Pact consumer (frontend) + provider (`forzion.tech.PactVerification`).
- `openapi-drift.yml` — `openapi:check` (tipos MSW vs OpenAPI; `git diff --exit-code`).
- `semgrep.yml`, `zap.yml` — SAST + DAST. `lighthouse.yml` — perf/a11y (LHCI). `hygiene.yml` — `knip` (dead code) + `madge` (ciclos). `smoke.yml` — Playwright smoke pós-deploy homolog.

## 8. THRESHOLDS DE COBERTURA (detectado — NÃO abaixar sem aprovação humana)
- **Backend (Coverlet → ReportGenerator JsonSummary → `scripts/check-coverage.sh`, por assembly, em CI)**: Domain branch 75 / line+method 85; Application branch 75 / line+method 85; Api line 85 / method 70; global branch 50; Infrastructure branch 35 (exclui Migrations). Baselines reais acima dos pisos (ver comentários no `ci.yml`) — pisos guardam regressão + execução silenciosa. Avaliação de TODOS os thresholds num único relatório (1 `dotnet test` por job; não mais `/p:Threshold` por assembly).
- **Frontend (vitest, por glob, ENFORCED em `vitest run --coverage` = exit≠0)**: `src/lib/**` 95/90/95/95 (l/b/f/s); `src/hooks/**` 90/85/90/90; `src/components/**` 85/75/85/85; `src/app/api/**` 90/85/90/90; `src/app/**` 70/60/55/65. Excluídos: `src/test/**`, `src/types/**`, `*.config.*`, `*.stories.tsx`, `*.property.test.ts`, `__tests__/`, `e2e/`.
- **Mutation (Stryker.NET)**: break 40 / low 60 / high 80 (Domain+Application; ignora Migrations).

## 9. COMO RODAR (comandos)
- Backend unit (sem Docker): `dotnet test forzion.tech.Tests --filter "Category!=Integration"`. Integração (Docker up): `--filter "Category=Integration"`. Tudo: sem filtro. Cobertura no CI: 1 `dotnet test ... /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura` por job → `reportgenerator -reporttypes:JsonSummary` → `bash scripts/check-coverage.sh` (thresholds; ver ci.yml). Mutation: `dotnet stryker` (ou matriz `--project`).
- Frontend: `npm test` (3 projects) · `test:unit|integration|api` · `test:coverage` · `test:property` · `test:contract` · `test:mutation`. E2E: `npm run e2e` (precisa app rodando + `E2E_*` creds; `e2e:install` 1x; `e2e:smoke`/`:security`/`:lgpd`; `e2e:update-snapshots` SÓ no CI Linux). Storybook: `storybook:build`/`:test`.

## 10. GOTCHAS
- **CRLF / SonarAnalyzer (Windows)**: ver [specification-git] §EDGE CASES (CANÔNICO) — ENDOFLINE, S3267, sequência de fix.
- **Docker obrigatório** p/ integração/Testcontainers e p/ stack local E2E. Sem Docker: só unit + vitest. Backend em Development migra/seeda o REMOTO — usar docker-compose local (schema `develop`).
- **Visual snapshots Playwright**: baseline por-OS; gerados no CI (Linux). Local Windows/macOS diverge — NÃO commitar baseline local; excluir `e2e/visual` em runs locais.
- **vitest projects**: alguns testes em `src/lib/**` que tocam DOM rodam no project `integration` (lista de include/exclude explícita) — respeitar ao adicionar testes.
- **E2E fail-loud**: `auth.setup` exige `E2E_{ADMIN,ALUNO,TREINADOR}_{EMAIL,PASSWORD}` e os specs checam `e2e/.auth/<role>.json` em tempo de coleta → rodar `--project=setup` primeiro. Seletores `getByLabel` ancorados são frágeis (preferir `data-testid`/exact).
- **Cobertura consolidada (CI)**: 1 `dotnet test` por job → `reportgenerator -reporttypes:JsonSummary` → `scripts/check-coverage.sh` avalia thresholds por assembly de UM relatório. NÃO re-rodar `dotnet test` por assembly/threshold. `check-coverage.sh` exige `jq` (presente no ubuntu-latest). Path-filter (`changes` job) pula jobs por área — gate usa `if: always()` + agregação `needs.*.result` (pulado ≠ falha).

## 11. REFERÊNCIAS
[specification-backend] (camadas/handlers), [specification-db] (Testcontainers/schemas), [specification-git] (CRLF/format/commits), [specification-stripe] (FakeStripeService/webhook), [specification-email]/[specification-whatsapp] (Null* + decorators de teste), [specification-security] (semgrep/zap/dep-scan gates), [specification-observability] (lighthouse budgets), [specification-frontend-ui] (harness a11y), [specification-local-ci-repro] (reproduzir gates local + gotchas; achado Application coverage <85 RESOLVIDO — method 95.86%, commit 947ff91). Relatório de validação ao vivo + gaps: `.specs/qa/validation-report-2026-05-29.md`.
