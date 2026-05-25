# Backend Test Harness — Quebra de Tasks

**Spec**: `docs/backend-harness-plan.md` (roadmap 13 fases, full parity)
**Status**: ✅ F0 (#39) · F1 (#42) · F2 (split unit/integration) · F3–F6 (#44) · F8–F9–F12 (#43) · F10 (#40) · F11 (#41) — mergeadas em `backend`. **Pendente: F7 (E2E real, depende de F2).**

Quebra atômica e executável da spec aprovada. Cada task tem entregável único,
dependências explícitas, critério de pronto verificável e **guard rails**.

---

## Convenção de execução

- **1 branch + 1 PR por fase** → base `backend`. Branch `chore/backend-harness-faseN-<slug>`.
- Conventional Commits, scope `backend`. Pre-commit + CI verdes antes de mergear.
- Suíte verde **a cada task** — não acumular quebrado.
- Tasks `[P]` = paralelizáveis (sem dependência mútua, sem estado compartilhado).

## Gates (comandos canônicos)

| Gate | Comando | Critério |
|------|---------|----------|
| **build** | `dotnet build forzion.tech.slnx -c Release` + `dotnet format forzion.tech.slnx --verify-no-changes` | 0 warning / 0 error / format limpo |
| **quick** | build + `dotnet test forzion.tech.Tests --filter "Category!=Integration"` (sem Docker) | 999 testes verdes |
| **full** | build + `dotnet test forzion.tech.Tests` (suíte inteira, exige Docker) | 1061 testes verdes (999 unit + 62 integração) |

## Guard rails globais (valem para TODA task)

1. **No silent deletions**: contagem de testes nunca cai sem justificativa explícita no PR.
2. **Não enfraquecer gates existentes**: cobertura branch (global 50 / Domain 75 / Application 75), Pact provider, Semgrep, `dotnet format` continuam ativos e verdes.
3. **Migrations data-preserving**: sempre `RenameTable`/`RenameColumn`/`RenameIndex`/`Sql(...)`; **nunca** drop+create de tabela com dados. Schema `homolog`. Gerar com `ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations add <Nome> -p forzion.tech.Infrastructure -s forzion.tech.Api`. Revisar o scaffold antes de commitar.
4. **Escopo**: não tocar `frontend/` nem `e2e/` (salvo task que exija explicitamente). Não mexer em `Assinante` (projeção billing) ao falar de `AssinaturaAluno`.
5. **Dependências novas**: fixar versão estável, `PrivateAssets=all` em analyzers/tools, validar API real via **context7** antes de usar (não fabricar API).
6. **Determinismo (Fase 1)**: trocar só a *fonte* de tempo/id — nunca alterar comportamento observável nem contrato JSON/rotas.
7. **Cada PR ≤ ~1 fase**: não misturar fases. Reverter é barato quando a fase é isolada.

---

## Plano de execução (grafo de fases)

```
F0 ✅
  └→ F1 ✅ (determinismo, fundacional)
        ├→ F2 ✅ (split unit/integration)
        ├→ F3 ✅ (arch tests)
        ├→ F4 ✅ (test builders)
        ├→ F5 ✅ (property-based)
        └→ F8 ✅ (mutation CI)
  F2 └→ F7 ⬜ (E2E real)               ← PENDENTE (depende de F2 ✅)
  F0 └→ F6 ✅ (snapshot/Verify)
  F0 └→ F9 ✅ (cobertura)
  F0 └→ F10 ✅ (supply-chain NuGet)
  F0 └→ F11 ✅ (pre-commit backend)
  F0 └→ F12 ✅ (openapi drift)
```

> **Resta só F7.** Todas as outras fases estão mergeadas em `backend`.

Fases independentes de F1 (F6, F9, F10, F11, F12) podem ser feitas em qualquer ordem após F0.
F5 e F8 **exigem** F1. F7 exige F2.

---

## Fase 0 — Higiene de tooling + baseline ✅

> Concluída no PR #39 (`a27a1d9`). Listada para rastreabilidade.

### F0.1 — Directory.Build.props central ✅
**What**: Centralizar TFM/ImplicitUsings/Nullable/LangVersion/AnalysisLevel/EnforceCodeStyleInBuild; remover duplicação dos 6 csprojs.
**Where**: `Directory.Build.props` (novo), `forzion.tech.*/*.csproj`.
**Done**: build 0/0; `WarningsAsErrors=nullable`.
**Gate**: build ✅

### F0.2 — Gate `dotnet format` + EOL determinístico ✅
**What**: Step `dotnet format --verify-no-changes` no `ci.yml`; `.gitattributes` `*.cs eol=crlf`; Migrations exemptas de analyzers no `.editorconfig`.
**Where**: `.github/workflows/ci.yml`, `.gitattributes`, `.editorconfig`.
**Done**: format verify verde local + CI.
**Gate**: build ✅

---

## Fase 1 — Determinismo (TimeProvider + IGuidProvider) *(fundacional)* ✅

**Branch**: `chore/backend-harness-fase1-determinismo` · **Commit**: `refactor(backend): injeta TimeProvider no dominio (harness fase 1)`

```
F1.1 → F1.2 → F1.3 → F1.4 → F1.6
                              ↑
                    F1.5 [P opcional]
```

### F1.1 — Registrar TimeProvider + FakeTimeProvider nos testes
**What**: Registrar `TimeProvider.System` no DI; adicionar `Microsoft.Extensions.TimeProvider.Testing` ao projeto de testes.
**Where**: `forzion.tech.Infrastructure/DependencyInjection/InfrastructureExtensions.cs`, `forzion.tech.Tests/forzion.tech.Tests.csproj`.
**Depends on**: F0
**Done when**:
- [ ] `services.AddSingleton(TimeProvider.System)` registrado.
- [ ] Pacote `Microsoft.Extensions.TimeProvider.Testing` (versão estável; validar via context7) referenciado em Tests.
- [ ] Gate **quick** verde.
**Guard rails**: usar `TimeProvider` do BCL (.NET 8), não criar `IClock` próprio; não mudar nenhum comportamento ainda (só registrar).
**Tests**: none (wiring) · **Gate**: quick

### F1.2 — Factories Coaching aceitam timestamp
**What**: `Criar()` de Aluno, Treinador, VinculoTreinadorAluno, Conta, SystemUser, PlanoPlataforma, Pacote, LogAprovacao recebem `DateTime` (ou `DateTimeOffset`) em vez de `DateTime.UtcNow` inline; callers (handlers/seed) passam `timeProvider.GetUtcNow().UtcDateTime`.
**Where**: `forzion.tech.Domain/Entities/*.cs` (grupo coaching) + handlers em `forzion.tech.Application/UseCases/**` + `DataSeeder`.
**Depends on**: F1.1
**Done when**:
- [ ] Zero `DateTime.UtcNow` nas factories desse grupo.
- [ ] Todos os callers passam o tempo via `TimeProvider`.
- [ ] Gate **quick** verde (912), sem mudança de asserção de comportamento.
**Guard rails**: não alterar API pública/DTO/rotas; eventos de domínio inalterados; um grupo por PR-task pra revisão; suíte verde antes de seguir.
**Tests**: unit · **Gate**: quick

### F1.3 — Factories Billing aceitam timestamp
**What**: Idem F1.2 para AssinaturaAluno, Pagamento, ContaRecebimento, Assinante.
**Where**: `forzion.tech.Domain/Entities/{AssinaturaAluno,Pagamento,ContaRecebimento,Assinante}.cs` + handlers de pagamento/assinatura/onboarding.
**Depends on**: F1.2
**Done when**:
- [ ] Zero `DateTime.UtcNow` nas factories billing.
- [ ] `DataProximaCobranca`/`PixExpiracao` derivam do `TimeProvider`.
- [ ] Gate **quick** verde.
**Guard rails**: `AssinaturaAluno.AgendarProximaCobranca` continua validando "futuro" — passar `TimeProvider` para o handler, não hardcode. Não confundir `Assinante` (projeção) com `AssinaturaAluno`.
**Tests**: unit · **Gate**: quick

### F1.4 — Factories Training aceitam timestamp
**What**: Idem para Treino, TreinoExercicio, SerieConfig, Exercicio, GrupoMuscular, TreinoAluno, ExecucaoTreino, ExecucaoExercicio.
**Where**: `forzion.tech.Domain/Entities/*.cs` (grupo training) + handlers de treino/execução.
**Depends on**: F1.3
**Done when**:
- [ ] Zero `DateTime.UtcNow` nas factories training.
- [ ] Gate **full** verde (inclui repos Docker que tocam essas entidades).
**Guard rails**: `RegistrarExecucao` usa data fornecida pelo cliente quando aplicável — não sobrescrever com `TimeProvider` indevidamente.
**Tests**: integration · **Gate**: full

### F1.5 — IGuidProvider (opcional) [P]
**What**: Abstração `IGuidProvider` (default `Guid.NewGuid`) injetável onde teste exige id determinístico.
**Where**: `forzion.tech.Application` (interface) + `Infrastructure` (impl + DI).
**Depends on**: F1.1
**Done when**:
- [ ] Interface + impl default registradas; usada em ≥1 ponto que se beneficia.
- [ ] Gate **quick** verde.
**Guard rails**: opcional — só adotar onde agrega; não trocar todos os `Guid.NewGuid` cegamente (ids não precisam ser determinísticos na maioria dos casos).
**Tests**: unit · **Gate**: quick

### F1.6 — Testes de lógica temporal
**What**: Testes novos usando `FakeTimeProvider` para cenários temporais (próxima cobrança, expiração Pix, janelas de assinatura).
**Where**: `forzion.tech.Tests/Domain` + `/Application`.
**Depends on**: F1.3, F1.4
**Done when**:
- [ ] ≥3 cenários temporais cobertos com tempo controlado.
- [ ] Gate **quick** verde (contagem sobe).
**Guard rails**: usar `FakeTimeProvider.Advance(...)`; não usar `Thread.Sleep`/tempo real.
**Tests**: unit · **Gate**: quick

---

## Fase 2 — Split unit vs integration ✅

**Branch**: `chore/backend-harness-fase2-split-tests`

### F2.1 — Marcar testes de integração com Trait ✅
**What**: Anotar testes que usam `InfrastructureTestFixture`/Testcontainers com `[Trait("Category","Integration")]`.
**Where**: `forzion.tech.Tests/Infrastructure/Repositories/*` (os 5 que usam `[Collection(InfrastructureTestCollection.Name)]`).
**Depends on**: F0
**Done when**:
- [x] Todos os testes Docker marcados (só os 5 RepositoryTests usam Testcontainers; Notifications/Integration são mock-based e ficam no conjunto rápido).
- [x] `dotnet test --filter "Category!=Integration"` roda só os rápidos (999 verdes, ~3s, sem Docker).
- [x] `dotnet test --filter "Category=Integration"` seleciona exatamente os 5 RepositoryTests (62 testes).
**Guard rails**: filtro por Trait; o filtro por nome antigo continua funcionando (nomes inalterados). Split disjunto e completo: 999 unit + 62 integração.
**Tests**: none (anotação) · **Gate**: quick

### F2.2 — Jobs CI separados (unit rápido + integration) ✅
**What**: Quebrar o job `test-backend` em `test-backend-unit` (sem Docker, todo PR) e `test-backend-integration` (Testcontainers).
**Where**: `.github/workflows/ci.yml`.
**Depends on**: F2.1
**Done when**:
- [x] Job unit usa `--filter "Category!=Integration"`; gates Domain/App (branch 75 + line/method 85) e Api (line 85 + method 70) — todos sustentados só por unit (verificado local).
- [x] Job integration roda a suíte COMPLETA com Docker; gates global (50 branch) + Infra (35 branch) + summary/report HTML.
- [x] `gate.needs` atualizado pros dois jobs; required check (`gate`) inalterado.
**Decisão de design**: o job de integração roda a suíte inteira (não só `Category=Integration`) porque os gates global/Infra só fecham com a cobertura da UNIÃO unit+integração — medir só os 62 derrubaria ambos. Os 3 gates de cobertura (50/75/75) ficam distribuídos: 75/75 no unit, 50 no integration; Infra 35 no integration.
**Guard rails**: 3 gates de cobertura intactos e distribuídos corretamente; cobertura de Infra preservada.
**Tests**: none (CI) · **Gate**: full

---

## Fase 3 — Architecture tests (NetArchTest.Rules) [P com F2/F4] ✅

**Branch**: `chore/backend-harness-fase3-arch-tests`

### F3.1 — Projeto/área de arch tests + pacote
**What**: Adicionar `NetArchTest.Rules` ao Tests e criar `Architecture/LayeringTests.cs`.
**Where**: `forzion.tech.Tests/Architecture/`, `forzion.tech.Tests.csproj`.
**Depends on**: F0
**Done when**:
- [ ] Pacote referenciado (versão estável; validar via context7).
- [ ] Arquivo de teste compila e roda.
- [ ] Gate **quick** verde.
**Guard rails**: não usar reflection frágil; carregar assemblies via tipos âncora (`typeof(Aluno).Assembly`).
**Tests**: unit · **Gate**: quick

### F3.2 — Regras de direção de dependência
**What**: Testes: Domain não referencia Application/Infrastructure/Api/EFCore; Application só Domain; Infra não referenciada por Domain/App.
**Where**: `forzion.tech.Tests/Architecture/LayeringTests.cs`.
**Depends on**: F3.1
**Done when**:
- [ ] 4+ regras verdes contra o código atual.
- [ ] Violação proposital (teste temporário) falha — comprovado e revertido.
- [ ] Gate **quick** verde.
**Guard rails**: começar pelas regras de maior valor (direção + "EF fora do Domain"); evitar falso-positivo em utilitários.
**Tests**: unit · **Gate**: quick

### F3.3 — Regras de convenção de domínio
**What**: Entidades com setters privados; construção só via factory `Criar`; handlers seguem sufixo/namespace.
**Where**: `forzion.tech.Tests/Architecture/ConventionTests.cs`.
**Depends on**: F3.1
**Done when**:
- [ ] Regras de setter privado + naming verdes.
- [ ] Gate **quick** verde.
**Guard rails**: se uma regra gerar falso-positivo legítimo, documentar exceção explícita, não afrouxar a regra toda.
**Tests**: unit · **Gate**: quick

---

## Fase 4 — Test data builders [P com F2/F3] ✅

**Branch**: `chore/backend-harness-fase4-builders`

### F4.1 — Builders das entidades core
**What**: `AlunoBuilder`, `TreinadorBuilder`, `PacoteBuilder`, `AssinaturaAlunoBuilder`, etc. com defaults válidos + overrides, determinísticos (seed fixo).
**Where**: `forzion.tech.Tests/Builders/`.
**Depends on**: F0 (idealmente após F1 para passar `TimeProvider`)
**Done when**:
- [ ] Builders cobrem as entidades mais construídas nos testes.
- [ ] Seed fixo → saída reproduzível.
- [ ] Gate **quick** verde.
**Guard rails**: builder à mão OU AutoFixture/Bogus — escolher UM e padronizar; não misturar; não duplicar regra de validação do domínio.
**Tests**: unit · **Gate**: quick

### F4.2 — Migrar testes de maior churn para builders
**What**: Substituir construção manual repetida (vista nos refactors recentes) pelos builders.
**Where**: `forzion.tech.Tests/**` (alvos de maior repetição).
**Depends on**: F4.1
**Done when**:
- [ ] Construção manual repetida eliminada nos alvos.
- [ ] Gate **quick** verde, sem regressão de cobertura.
**Guard rails**: migração mecânica; não alterar o que cada teste verifica.
**Tests**: unit · **Gate**: quick

---

## Fase 5 — Property-based testing (CsCheck) — exige F1 ✅

**Branch**: `chore/backend-harness-fase5-property`

### F5.1 — Pacote + properties do Email VO
**What**: Add `CsCheck`; properties para `Email` (normalização lower/trim, limites, idempotência de `Criar`).
**Where**: `forzion.tech.Tests/Domain/Properties/EmailProperties.cs`, `forzion.tech.Tests.csproj`.
**Depends on**: F1
**Done when**:
- [ ] CsCheck referenciado (versão estável; validar via context7).
- [ ] Properties verdes; gerador alinhado ao regex do VO (não mais permissivo).
- [ ] Gate **quick** verde.
**Guard rails**: arbitrary não pode gerar inputs que o VO aceita mas o regex rejeita (documentar gap se houver); seed logado em falha.
**Tests**: unit · **Gate**: quick

### F5.2 — Properties de Result<T> e invariantes de entidade
**What**: Leis de `Result<T>` (map/bind/sucesso-falha); invariantes de `Criar` (Guid vazio rejeitado, limites de string, `Valor>0`).
**Where**: `forzion.tech.Tests/Application/Properties/`, `/Domain/Properties/`.
**Depends on**: F5.1
**Done when**:
- [ ] ≥1 property por entidade core + Result<T>.
- [ ] Gate **quick** verde.
**Guard rails**: não testar implementação, só propriedades observáveis; manter runs default (≈100) salvo auth/pagamento (subir).
**Tests**: unit · **Gate**: quick

---

## Fase 6 — Snapshot / contract de saída (Verify.Xunit) [P] ✅

**Branch**: `chore/backend-harness-fase6-verify`

### F6.1 — Verify + snapshot dos response DTOs
**What**: Add `Verify.Xunit`; snapshots dos principais response records.
**Where**: `forzion.tech.Tests/Api/Snapshots/`.
**Depends on**: F0
**Done when**:
- [ ] Verify referenciado; snapshots `.received/.verified` configurados (gitignore do `.received`).
- [ ] Snapshots aprovados para DTOs core.
- [ ] Gate **quick** verde.
**Guard rails**: snapshot só de contrato estável; não snapshotar payload gigante/volátil; `.verified` versionado, `.received` ignorado.
**Tests**: unit · **Gate**: quick

### F6.2 — Snapshot do mapa exceção→ProblemDetails
**What**: Snapshot do `GlobalExceptionHandler` (cada exceção → status/ProblemDetails).
**Where**: `forzion.tech.Tests/Api/Snapshots/GlobalExceptionHandlerSnapshots.cs`.
**Depends on**: F6.1
**Done when**:
- [ ] Cada exceção de domínio mapeada tem snapshot.
- [ ] Gate **quick** verde.
**Guard rails**: mudança de mapeamento exige re-aprovar snapshot conscientemente (é o sinal desejado).
**Tests**: unit · **Gate**: quick

---

## Fase 7 — Integração / E2E real pela pipeline — exige F2 ⬜ PENDENTE

**Branch**: `chore/backend-harness-fase7-e2e-real`

### F7.1 — Fixture WebApplicationFactory + Postgres real
**What**: Factory que sobe a app com handlers **reais** (não mockados) + Testcontainers Postgres + DB semeado.
**Where**: `forzion.tech.Tests/E2E/RealPipelineFixture.cs`.
**Depends on**: F2
**Done when**:
- [ ] Factory inicia app real apontando pro container; migrations/EnsureCreated aplicados.
- [ ] 1 teste smoke (health/endpoint público) verde.
- [ ] Gate **full** verde.
**Guard rails**: marcar `Category=Integration`; não mockar handler; isolar DB por teste/coleção.
**Tests**: integration · **Gate**: full

### F7.2 — Fluxos críticos E2E
**What**: Testes ponta-a-ponta: cadastro aluno/treinador; aprovação de vínculo → criação de `AssinaturaAluno`; geração de cobrança.
**Where**: `forzion.tech.Tests/E2E/`.
**Depends on**: F7.1
**Done when**:
- [ ] ≥3 fluxos críticos verdes E2E reais.
- [ ] ≥1 fluxo toca migração/persistência real.
- [ ] Gate **full** verde.
**Guard rails**: Stripe/serviços externos via fake/stub (não chamar Stripe real); não duplicar o que o teste de handler já cobre — focar em wiring/serialização/persistência.
**Tests**: integration · **Gate**: full

---

## Fase 8 — Mutation testing em CI (Stryker.NET) — exige F1 ✅

**Branch**: `chore/backend-harness-fase8-mutation`

### F8.1 — Expandir escopo do Stryker p/ Application + subir break
**What**: `stryker-config.json` inclui `forzion.tech.Application`; `break` realista (ex.: 60).
**Where**: `stryker-config.json`.
**Depends on**: F1 (idealmente F5)
**Done when**:
- [ ] Config cobre Domain + Application.
- [ ] Run local `dotnet stryker` produz score; acima do `break`.
- [ ] Gate **quick** verde (config não afeta build).
**Guard rails**: rodar local antes de plugar no CI; `break` começa conservador e sobe; não incluir Infra/Api (lento/baixo valor inicial).
**Tests**: none (config) · **Gate**: quick

### F8.2 — Job backend no mutation.yml
**What**: Adicionar job agendado de mutation backend (`dotnet stryker`) ao `mutation.yml`.
**Where**: `.github/workflows/mutation.yml`.
**Depends on**: F8.1
**Done when**:
- [ ] Job backend agendado (semanal) + manual; publica report.
- [ ] Run verde no schedule/manual.
**Guard rails**: NUNCA em todo PR (caro); agendado/manual; falha abaixo do `break`.
**Tests**: none (CI) · **Gate**: build

---

## Fase 9 — Endurecimento de cobertura [P] ✅

**Branch**: `chore/backend-harness-fase9-coverage`

### F9.1 — Thresholds line/method + gate Infra/Api
**What**: Adicionar thresholds de line/method (além de branch) e gate dedicado para Infrastructure/Api.
**Where**: `.github/workflows/ci.yml`, `forzion.tech.Tests/coverage.runsettings`.
**Depends on**: F0
**Done when**:
- [ ] Thresholds line/method ativos; Infra/Api com gate próprio (valor inicial = baseline atual).
- [ ] CI verde sem baixar nenhum threshold existente.
**Guard rails**: subir threshold só com testes que sustentem; nunca abaixar os atuais; faseado.
**Tests**: none (CI) · **Gate**: full

### F9.2 — Relatório ReportGenerator (HTML) no run
**What**: Gerar HTML de cobertura e publicar como artifact.
**Where**: `.github/workflows/ci.yml`.
**Depends on**: F9.1
**Done when**:
- [ ] `dotnet-reportgenerator-globaltool` gera HTML; artifact publicado.
- [ ] CI verde.
**Guard rails**: ferramenta como tool pinada; não inflar tempo do gate crítico (gerar em step separado/non-blocking).
**Tests**: none (CI) · **Gate**: build

---

## Fase 10 — Supply-chain + dependências NuGet [P] ✅

**Branch**: `chore/backend-harness-fase10-supply-chain`

### F10.1 — Gate de vulnerabilidade NuGet
**What**: Step CI `dotnet list package --vulnerable --include-transitive` que falha em vuln.
**Where**: `.github/workflows/ci.yml` (job security).
**Depends on**: F0
**Done when**:
- [ ] Step roda e falha se houver vuln (testado report-only → gate).
- [ ] CI verde (sem vuln atual).
**Guard rails**: começar report-only, depois promover a gate; transitivos podem gerar ruído — avaliar antes de bloquear.
**Tests**: none (CI) · **Gate**: build

### F10.2 — SBOM .NET (CycloneDX)
**What**: Gerar SBOM dos projetos .NET via CycloneDX.
**Where**: `.github/workflows/ci.yml`.
**Depends on**: F10.1
**Done when**:
- [ ] SBOM `.cdx` gerado como artifact.
- [ ] CI verde.
**Guard rails**: tool pinada; não publicar SBOM como release sem necessidade.
**Tests**: none (CI) · **Gate**: build

### F10.3 — Renovate gerencia NuGet
**What**: Adicionar manager NuGet ao `renovate.json` (grouping por área + automerge patch), espelhando npm.
**Where**: `renovate.json`.
**Depends on**: F0
**Done when**:
- [ ] Config NuGet válida; grouping definido; automerge só patch.
- [ ] Renovate abre PR de NuGet (após ativação).
**Guard rails**: `minimumReleaseAge` em majors; não automergear major; não incluir analyzers que quebrem build.
**Tests**: none (config) · **Gate**: build

---

## Fase 11 — Hygiene de commit/PR no backend [P] ✅

**Branch**: `chore/backend-harness-fase11-hooks`

### F11.1 — Pre-commit backend
**What**: Hook que, ao tocar arquivos backend, roda `dotnet format --verify` + build + testes unit rápidos.
**Where**: husky na raiz (ou extensão do `frontend/.husky`).
**Depends on**: F0 (idealmente F2 p/ gate unit rápido)
**Done when**:
- [ ] Commit backend que quebra format/build/unit é bloqueado localmente.
- [ ] Não atrasa commits que só tocam frontend.
**Guard rails**: rodar só unit rápidos (sem Docker) no pre-commit; cuidar do monorepo split (raiz .NET + `frontend/`); não duplicar o hook do frontend.
**Tests**: none (tooling) · **Gate**: quick

### F11.2 — Commitlint scope + CODEOWNERS backend
**What**: Garantir commitlint cobrindo scope `backend`; adicionar paths backend ao `CODEOWNERS`.
**Where**: config commitlint, `.github/CODEOWNERS`.
**Depends on**: F11.1
**Done when**:
- [ ] Commit com scope backend validado; paths backend com owner.
- [ ] CI verde.
**Guard rails**: não exigir review bloqueante sem combinar com o time.
**Tests**: none (config) · **Gate**: build

---

## Fase 12 — Fechamento de contrato / observabilidade *(opcional)* [P] ✅

**Branch**: `chore/backend-harness-fase12-openapi-drift`

### F12.1 — Versionar swagger + job openapi:check
**What**: Versionar o `swagger.json` gerado e job que falha o PR em drift não coordenado (espelha os tipos MSW do frontend).
**Where**: `.github/workflows/`, script de geração do swagger.
**Depends on**: F0
**Done when**:
- [ ] Swagger versionado; job detecta diff e falha.
- [ ] CI verde quando swagger está sincronizado.
**Guard rails**: drift é o sinal desejado — regenerar conscientemente; coordenar com `openapi:check` do frontend.
**Tests**: none (CI) · **Gate**: build

### F12.2 — Categorização + flaky detection (opcional)
**What**: Traits de categoria nos testes; re-run + report de flaky.
**Where**: `forzion.tech.Tests/**`, `.github/workflows/ci.yml`.
**Depends on**: F2
**Done when**:
- [ ] Testes categorizados; flaky reportado (não mascarado).
- [ ] CI verde.
**Guard rails**: re-run não pode esconder flake real — sempre reportar; não aumentar retry como "fix".
**Tests**: none · **Gate**: build

---

## Validação do breakdown

**Granularidade**: cada task = 1 entregável coeso (1 config / 1 grupo de factories coeso / 1 conjunto de regras / 1 job CI). Tasks de fronteira (F1.2–F1.4) agrupadas por bounded context para manter atomicidade revisável sem virar 21 micro-tasks.

**Co-locação de testes**: toda task que cria/altera camada de código inclui seus testes na mesma task (campo `Done when` + `Tests`). `Tests: none` só em tasks de wiring/config/CI — nunca difere teste de código para outra task.

**Cross-check dependências ↔ grafo**: dependências dos corpos das tasks batem com o grafo de fases no topo (F1 sequencial interno; F5/F8 dependem de F1; F7 depende de F2; F3/F4/F6/F9/F10/F11/F12 só de F0).

**Pendência antes de executar**: confirmar com o time o escopo de cada fase (sobretudo F1, que toca o Domain) e a escolha de ferramentas (builder à mão vs AutoFixture/Bogus; CsCheck vs FsCheck) — validar APIs reais via context7 no início de cada fase.
