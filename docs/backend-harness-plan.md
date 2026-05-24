# Backend Test Harness — Plano de Fases

Roadmap para levar o backend (.NET 8, clean architecture) à paridade de qualidade do
harness frontend (`frontend-harness-plan.md` / `frontend-harness-rationale.md`).

Estrutura espelha o frontend: **1 branch + 1 PR por fase**, Conventional Commits, gate
verde antes de avançar. Cada fase documenta **Objetivo / Por quê / Mudanças /
Trade-offs / Métricas de sucesso**.

Atualizado a cada fase concluída.

---

## Estado atual (baseline)

### Já existe

- Suíte **xUnit única** (`forzion.tech.Tests`, ~1003 testes) por camada: Domain /
  Application / Infrastructure / Api / Integration.
- Stack: xUnit, FluentAssertions, Moq, `Testcontainers.PostgreSql` (fixture via
  `EnsureCreatedAsync`), `coverlet` (collector + msbuild), `Microsoft.AspNetCore.Mvc.Testing`.
- **Cobertura gated em CI** (`.github/workflows/ci.yml`, job `test-backend`): branch
  global 50%, Domain 75%, Application 75% + `CodeCoverageSummary` no run.
- `SonarAnalyzer.CSharp` (Domain + Application), `.editorconfig`, `Nullable` enable.
- **Pact provider verification** (`forzion.tech.PactVerification` + `pact-provider.yml`):
  gated por broker, agendado terça 05:00 UTC + push em `homolog`.
- Semgrep (SAST) e Gitleaks (segredos) cobrindo a árvore.
- `stryker-config.json` existe (escopo Domain, `break: 0`) — **inerte**: não roda em CI
  (`mutation.yml` é só frontend).

### Lacunas (alvos do roadmap)

| # | Lacuna | Impacto |
|---|--------|---------|
| 1 | Sem abstração de tempo (`DateTime.UtcNow`/`Guid.NewGuid` inline em ~21 entidades) | Bloqueia mutation + property determinísticos; lógica temporal não testável |
| 2 | Sem architecture tests | Camadas limpas hoje, mas drift não detectado |
| 3 | Sem property-based testing | Edge cases de VO/invariantes escapam |
| 4 | Mutation não roda em CI; só Domain; `break: 0` | Qualidade de asserção não medida |
| 5 | Sem gate de estilo (`dotnet format`), sem `TreatWarningsAsErrors`/`AnalysisLevel` | Regressão de estilo/qualidade passa |
| 6 | Projeto de teste único (unit + integration juntos) | Sem feedback rápido; Docker no caminho crítico |
| 7 | Cobertura só branch e só Domain/App | Infrastructure/Api são pontos cegos |
| 8 | Supply-chain NuGet: sem scan de vulnerabilidade, sem SBOM, Renovate só npm | Risco de cadeia de suprimentos opaco |
| 9 | Sem pre-commit backend (husky é só frontend) | Commits backend não validados localmente |
| 10 | Sem snapshot de saída (Verify) nem E2E real (endpoints mockam handler) | Drift de contrato de resposta + bugs de wiring escapam |

---

## Fase 0 — Higiene de tooling + baseline

### Objetivo
Centralizar configuração de build/qualidade e tornar estilo/warnings verificáveis em CI
antes de qualquer expansão.

### Por quê
- Configuração hoje espalhada por csproj; `.editorconfig` existe mas **não é verificado**
  em automação — só disciplina local.
- Sem `TreatWarningsAsErrors`/`AnalysisLevel`, warnings de analyzer (Sonar/CA) não
  bloqueiam. Regressão de qualidade entra silenciosa.
- Baseline previsível é pré-requisito das fases seguintes (toda fase herda os mesmos flags).

### Mudanças
- `Directory.Build.props` na raiz: `Nullable=enable`, `LangVersion=latest`,
  `AnalysisLevel=latest`, `EnforceCodeStyleInBuild=true`, `TreatWarningsAsErrors`
  **faseado** (começa num subset de regras, expande).
- Endurecer severidades no `.editorconfig` (estilo → `warning`/`error` onde fizer sentido).
- Gate CI: `dotnet format --verify-no-changes`.

### Trade-offs
- `TreatWarningsAsErrors` global de uma vez quebraria o build com dívida pré-existente →
  começa por subset e endurece a cada fase.
- `dotnet format` pode divergir de preferências de IDE → `.editorconfig` é a fonte única.

### Métricas de sucesso
- `dotnet format --verify-no-changes` verde em CI.
- Build em `-c Release` sem warnings novos.
- ~1003 testes mantidos verdes.

---

## Fase 1 — Determinismo (TimeProvider + IGuidProvider) *(fundacional)*

### Objetivo
Eliminar não-determinismo de tempo/identidade do domínio, destravando mutation e
property testing confiáveis e permitindo testar lógica temporal.

### Por quê
- ~21 entidades chamam `DateTime.UtcNow`/`Guid.NewGuid()` direto nas factories `Criar()`.
  Cada execução produz estado diferente.
- **Mutation testing** fica inútil: mutante que altera lógica temporal pode "sobreviver"
  por causa de I/O não-determinístico.
- **Property testing** não reproduz falhas (seed varia).
- Lógica de negócio temporal não é testável hoje: `DataProximaCobranca`, expiração de Pix,
  janelas de assinatura.

### Mudanças
- Adotar `System.TimeProvider` (.NET 8, nativo) injetado na borda de Application/handlers.
- Factories `Criar()` recebem o timestamp (ou `TimeProvider`) em vez de `DateTime.UtcNow`
  inline; `IGuidProvider` opcional para ids determinísticos quando o teste exigir.
- Registro de `TimeProvider.System` no DI (`InfrastructureExtensions`); testes injetam
  `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`).
- Arquivos: `forzion.tech.Domain/Entities/*.cs`, handlers em
  `forzion.tech.Application/UseCases/**`, DI.

### Trade-offs
- Mexe nas factories do Domain (superfície grande) → fazer em PR dedicado, suíte verde a
  cada passo.
- Passar timestamp como parâmetro mantém o Domain puro (sem dependência de serviço), ao
  custo de mais um argumento em `Criar()`.

### Métricas de sucesso
- Zero `DateTime.UtcNow` em factories de domínio (tempo entra pela borda).
- Novos testes de lógica temporal (próxima cobrança, expiração Pix) com `FakeTimeProvider`.
- Suíte verde; pré-requisito da Fase 8 atendido.

---

## Fase 2 — Split unit vs integration

### Objetivo
Separar testes rápidos (sem Docker) dos de integração (Testcontainers) para feedback
sub-segundo no caminho crítico.

### Por quê
- Hoje tudo vive em `forzion.tech.Tests`; rodar a suíte exige Docker para os repos/integração.
- O gate "rápido" hoje é simulado com `--filter` ad-hoc (exclui `Infrastructure.Repositories`,
  `Integration`, `Notifications`).
- Espelha os Vitest projects do frontend (unit / integration / api).

### Mudanças
- Separar em `forzion.tech.Tests.Unit` (Domain + Application + Api-handler) e
  `forzion.tech.Tests.Integration` (Testcontainers), **ou** traits xUnit
  (`[Trait("Category","Integration")]`) + dois jobs CI.
- CI: job `unit` em todo PR (rápido, sem Docker); job `integration` com serviço Docker.

### Trade-offs
- Dois projetos = duplicação de helpers → extrair `forzion.tech.Tests.Common`. Traits
  evitam isso mas misturam categorias no mesmo assembly.

### Métricas de sucesso
- Gate unit < ~10s, sem Docker.
- Integration isolado, verde com Testcontainers.
- Zero `--filter` ad-hoc no CI.

---

## Fase 3 — Architecture tests (NetArchTest.Rules)

### Objetivo
Travar a clean architecture com testes executáveis, impedindo violações de camada.

### Por quê
- Direção de dependência hoje é correta (Domain sem deps; Application→Domain;
  Infra→App+Domain; Api→App+Infra) mas **não é enforced** — um `using` errado passa.
- Custo baixíssimo, confiança alta.

### Mudanças
- `forzion.tech.Tests/Architecture/*` com NetArchTest:
  - Domain não referencia Application/Infrastructure/Api/EF Core.
  - Application referencia só Domain (não Infrastructure).
  - Entidades têm setters privados; só factory `Criar` constrói.
  - Handlers seguem convenção de nome/namespace.

### Trade-offs
- Regras muito estritas geram falso-positivo em utilitários → começar pelas invariantes de
  maior valor (dep direction, EF fora do Domain).

### Métricas de sucesso
- Suite de arquitetura verde; violação proposital falha.
- Regras de direção de dependência + "EF fora do Domain" cobertas.

---

## Fase 4 — Test data builders (Object Mother / AutoFixture)

### Objetivo
Padronizar construção de entidades de teste, reduzindo boilerplate e churn quando o schema
cresce.

### Por quê
- Os 3 refactors recentes (ContaRecebimento, GrupoMuscular FK, rename) exigiram editar
  dezenas de construções manuais de entidade em testes.
- Builders determinísticos (seed fixo) sobrevivem a expansão de schema: teste passa só os
  campos que importam.

### Mudanças
- Builders por entidade core (`AlunoBuilder`, `TreinadorBuilder`, ...) com defaults válidos
  + overrides, **ou** `AutoFixture`/`Bogus` com seed fixo (alinha com determinismo da Fase 1).
- Migrar testes de maior churn para os builders.

### Trade-offs
- Builders à mão = mais código mas controle total; `AutoFixture` = menos código mas mágica
  de reflection. Escolher um e padronizar.

### Métricas de sucesso
- Construção manual repetida eliminada nos testes core.
- Sem regressão de cobertura; suíte verde.

---

## Fase 5 — Property-based testing (CsCheck)

### Objetivo
Verificar **propriedades universais** (invariantes) sobre lógica pura do domínio, cobrindo
espaços de input que testes por exemplo não alcançam.

### Por quê
- `Email` VO (normalização, limites, risco ReDoS), `Result<T>` (leis de map/bind),
  invariantes de `Criar()` (Guid vazio rejeitado, limites de string), `Valor > 0`, faixas
  de data são **funções puras** — caso ideal.
- Aumenta sensibilidade para a Fase 8 (mutation).

### Mudanças
- `CsCheck` (idiomático C#, leve) — properties para Email, `Result<T>`, invariantes de
  entidade, regras monetárias/temporais.
- Determinismo da Fase 1 garante reprodutibilidade (seed logado em falha).

### Trade-offs
- Arbitraries precisam refletir a semântica real (ex.: gerador de email não pode ser mais
  permissivo que o regex do VO) — documentar gaps.

### Métricas de sucesso
- ≥1 invariante por entidade core + Email + `Result<T>`.
- Properties verdes; bugs de edge case documentados se encontrados.

---

## Fase 6 — Snapshot / contract de saída (Verify.Xunit)

### Objetivo
Travar o shape dos DTOs de resposta e do mapeamento exceção→ProblemDetails, detectando
drift de contrato.

### Por quê
- Mudança acidental em response DTO ou no `GlobalExceptionHandler` (mapa exceção→HTTP) passa
  hoje sem alarme.
- Complementa o Pact (consumer-driven) com golden-files locais e rápidos.

### Mudanças
- `Verify.Xunit`: snapshots dos principais response DTOs e do mapa de exceções
  (`forzion.tech.Api/Middleware/GlobalExceptionHandler.cs`).
- Snapshots versionados; mudança exige re-aprovação explícita.

### Trade-offs
- Snapshots podem virar ruído se forem largos demais → limitar a contratos estáveis e de alto
  valor.

### Métricas de sucesso
- Snapshots aprovados para DTOs core + tabela de erros.
- Alteração de contrato falha o teste até re-aprovar.

---

## Fase 7 — Integração / E2E real pela pipeline

### Objetivo
Exercitar endpoints ponta-a-ponta com handlers reais, EF real e DB semeado — pegando bugs de
wiring/DI/serialização/migração que os testes com handler mockado não veem.

### Por quê
- Os endpoint tests atuais usam `WebApplicationFactory` mas **mockam os handlers** — validam
  roteamento/auth/validação, não o caminho real de dados.
- Bugs de DI, mapeamento EF, serialização JSON e migração só aparecem no fluxo real.

### Mudanças
- `WebApplicationFactory` + `Testcontainers.PostgreSql`, handlers reais, EF real, DB semeado.
- Cobrir fluxos críticos: cadastro de aluno/treinador, aprovação de vínculo → criação de
  `AssinaturaAluno`, geração de cobrança.

### Trade-offs
- Mais lentos (Docker + EF) → ficam no job `integration` (Fase 2), não no gate rápido.

### Métricas de sucesso
- Fluxos críticos verdes end-to-end, sem mock de handler.
- Pelo menos 1 fluxo que toca migração/persistência real.

---

## Fase 8 — Mutation testing em CI (Stryker.NET)

### Objetivo
Medir a qualidade real das asserções (não só cobertura) e plugar mutation no CI.

### Por quê
- `stryker-config.json` existe mas é inerte (`break: 0`, só Domain, fora do CI).
- Mutation só faz sentido após determinismo (Fase 1) — caso contrário mutantes sobrevivem por
  aleatoriedade.
- Cobertura alta ≠ asserção boa; mutation expõe testes que executam mas não verificam.

### Mudanças
- Expandir `stryker-config.json` para incluir `forzion.tech.Application`.
- Subir `break` (ex.: 60) — passa a falhar abaixo do limiar.
- Adicionar job backend ao `mutation.yml` (agendado, espelhando o frontend).

### Trade-offs
- Mutation é caro (minutos) → agendado (semanal) + manual, não em todo PR.

### Métricas de sucesso
- Mutation score acima do `break` em Domain + Application.
- Job backend visível no `mutation.yml`.

---

## Fase 9 — Endurecimento de cobertura

### Objetivo
Fechar os pontos cegos de cobertura (Infrastructure/Api) e ir além de branch-only.

### Por quê
- Hoje só branch coverage, e só Domain/App têm threshold dedicado; Infra/Api caem no global
  50%.
- Repositórios e endpoints carregam lógica crítica (persistência, auth) pouco medida.

### Mudanças
- Thresholds de line/method além de branch; gate dedicado para Infrastructure e Api.
- `ReportGenerator` (HTML) publicado como artifact; alvo por camada documentado.
- (Opcional) integração de tendência (codecov) se desejado.

### Trade-offs
- Subir thresholds de Infra/Api pode exigir testes novos primeiro → fasear o aperto.

### Métricas de sucesso
- Thresholds line/method ativos; Infra/Api com gate próprio.
- Relatório de cobertura no run.

---

## Fase 10 — Supply-chain + automação de dependências (NuGet)

### Objetivo
Trazer pro backend o mesmo rigor de cadeia de suprimentos que o frontend já tem (OSV, SBOM,
audit, Renovate).

### Por quê
- O job de segurança do CI cobre só npm (OSV, audit, license, SBOM). NuGet fica sem scan.
- Renovate gerencia só npm — pacotes .NET são atualizados na mão.

### Mudanças
- CI: `dotnet list package --vulnerable --include-transitive` como gate.
- SBOM .NET (CycloneDX `dotnet-CycloneDX`); license check de NuGet.
- `renovate.json`: adicionar gerenciamento NuGet (grouping por área + automerge patch),
  espelhando a config npm.

### Trade-offs
- `--vulnerable` depende do feed do NuGet estar atualizado; transitivos podem gerar ruído →
  começar report-only, depois gate.

### Métricas de sucesso
- Scan de vulnerabilidade NuGet verde no CI.
- SBOM .NET gerado; Renovate abrindo PRs de NuGet.

---

## Fase 11 — Hygiene de commit/PR no backend

### Objetivo
Validar commits backend localmente (hoje só o frontend tem hooks).

### Por quê
- `husky` vive em `frontend/`; mudanças backend não passam por pre-commit (build/test/format).
- `commitlint` roda em CI mas o hook `commit-msg` é frontend-only.

### Mudanças
- Hook na raiz (ou estender o husky) que, quando há mudança backend, roda
  `dotnet format --verify` + build + testes unit rápidos.
- `commitlint` cobrindo scope `backend`; `CODEOWNERS` para paths backend.

### Trade-offs
- Hook .NET no pre-commit adiciona latência → rodar só unit rápidos (Fase 2), não integração.
- Monorepo split (raiz .NET + `frontend/`) exige cuidado na config do husky.

### Métricas de sucesso
- Commit backend que quebra format/build/test é bloqueado localmente.
- `CODEOWNERS` cobre paths backend.

---

## Fase 12 — Fechamento de contrato / observabilidade *(opcional)*

### Objetivo
Detectar drift de contrato backend↔frontend automaticamente e refinar sinais de qualidade.

### Por quê
- O frontend gera tipos MSW do swagger; renome/alteração de campo no backend hoje só é pego
  pela verificação Pact ou manualmente (visto no rename plataforma/aluno).
- Categorização de testes e detecção de flaky fecham a maturidade do harness.

### Mudanças
- Job `openapi:check`: versiona o swagger gerado e falha o PR se houver drift não coordenado.
- Traits/categorização de testes; detecção de flaky (re-run + report).

### Trade-offs
- Versionar o swagger gera diff a cada mudança de API → é justamente o sinal desejado, mas
  exige disciplina de regenerar.

### Métricas de sucesso
- Drift de OpenAPI falha o PR.
- Testes categorizados; flaky reportado.

---

## Referências

- Precedente de formato: `docs/frontend-harness-plan.md`, `docs/frontend-harness-rationale.md`.
- CI: `.github/workflows/ci.yml` (job `test-backend`), `pact-provider.yml`, `mutation.yml`,
  `semgrep.yml`, `contract.yml`.
- Tooling: `stryker-config.json`, `.editorconfig`, `forzion.tech.Tests/coverage.runsettings`,
  `renovate.json`.
- Testes: `forzion.tech.Tests/`, `forzion.tech.Tests/Infrastructure/InfrastructureTestFixture.cs`.
- Domínio a tornar determinístico: `forzion.tech.Domain/Entities/*.cs`,
  `forzion.tech.Domain/ValueObjects/Email.cs`, `forzion.tech.Application/Results/Result.cs`.
