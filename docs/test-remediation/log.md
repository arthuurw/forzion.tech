# Test Remediation — Session Log

> Append-only. Uma entrada por sessão. Sempre topo mais recente.

---

## 2026-05-28 — F12 fechado após Docker up (Fase 3 100% das tasks com Docker)

**Sessão:** Continuação. Usuário trouxe Docker Desktop online.

**Feito (1 commit em `fix/frontend-dockerfile-npmrc`):**

1. **F12 Concurrent billing race** (TBD commit) `test(backend): cover GerarCobrancaMensal race via Testcontainers (F12)`
   - Novo `forzion.tech.Tests/E2E/ConcurrentBillingRaceTests.cs` (categoria Integration).
   - Setup ponta-a-ponta via API pública: register treinador → verify email → admin aprova → atribui plano Free → onboarding Stripe (fake) → criar pacote → register aluno → admin obtém vinculoId → treinador aprova vinculo (gera AssinaturaAluno via domain event).
   - Race spawnada por `Barrier(2)` sincronizando 2 Tasks. Cada Task cria DI scope próprio (DbContext isolado), resolve GerarCobrancaMensalHandler, chama HandleAsync com MESMO command.
   - Assertions: (a) ≥1 success; (b) qualquer failure deve ter PostgresException com SqlState "40001" (serialization failure aceitável, propagada via tx serializable); (c) DB tem ≤1 pagamento pendente para a assinatura; (d) se 2 successes, mesmo PagamentoId (idempotência via re-uso de pendente).
   - Run real (Testcontainers + Docker Desktop): 1 pagamento gerado, log mostra uma única "Cobrança Pix gerada". A 2a chamada entrou na transação serializable e (com base no log) ou hit a idempotent path ou foi rejeitada por race — invariantes mantidos. Duração ~10s.

**Decisões:**
- Não usei `[Trait("Category", "Quarantine")]` apesar do plano sugerir começar quarantined — primeiro run foi estável, sem flake. Reabrir como quarantined SE notar flake em CI.
- Helpers replicados de `FluxosCriticosE2ETests` (não extraí pra base class) — overhead de refactor maior que o ganho com 1 reuse. Se uma 3ª classe E2E precisar dos mesmos helpers, extrair base class então.
- Test categoria `Integration` — fora do pre-commit local (gate `Category!=Integration`); CI roda full suite com Docker no `test-backend-integration` job.

**Métricas:**
- Backend tests total: 1200 pass (+1 F12).
- F12 build: clean.
- F12 run: PASS em 10s.

**Métricas pós-sessão:** Total findings 38 — done 16, deferred 2, pending 20.

**Próximos passos (Fase 4):**
- F6 começar pelo menor (saude page.client.test.tsx 91 LOC).
- F3, F7, F16, F25, F26, F28, F29, F30 — vide state.md.
- F5 provider-side completar (~30 min).
- F17 admin-side 8 restantes.

---

## 2026-05-28 — Fase 3 implementada e fechada parcialmente

**Sessão:** Continuação direta após Fase 2 (mesmo dia, retomada via /loop após /compact).

**Feito (1 commit + 1 pendente em `fix/frontend-dockerfile-npmrc`):**

1. **F4 + F5 + F8 + F27** (`90040c0`) `test(backend,frontend): close F4 F5 F8 F27 in Fase 3 gate-hardening`
   - **F4 Pact provider repo-level**: convertidos Fichas/Vinculo/Perfil (era handler-level mock). Adicionados 6 mocks de repo: `BuildContaRepositoryMock`, `BuildAlunoRepositoryMock` (unificado: cobre `ListarTodosAsync` admin + `ObterPorContaIdAsync` perfil), `BuildTreinadorRepositoryMock`, `BuildVinculoRepositoryMock`, `BuildTreinoAlunoRepositoryMock`, `BuildExercicioRepositoryMock`. Handlers reais executam — mudança de DTO/conversão quebra Pact.
   - **F5 Pact error contracts (consumer-side)**: novo `frontend/src/test/pact/consumer-errors.test.ts` com 12 contratos (401/404/500 × 4 endpoints). Wrapper `captureError()` falha duro se chamada não throw (defesa contra regression). Output em `pacts-errors/` (gitignored, separado do `pacts/` publicado) até state handlers do provider serem implementados.
   - **F8 Stryker scope**: backend matrix `Domain + Application` já existia em `mutation.yml` via `--project` override — documentado no config. Frontend: `src/components/**` incluído no `mutate`; break threshold 75 → 60 (runway para components até baseline). Mutation continua weekly via cron.
   - **F27 Coverage enforcement**: vitest 4 `coverage.thresholds.<glob>` per-path enforce automaticamente quando `--coverage` roda — CI job `test-frontend` já chama `npm run test:coverage` (= `vitest run --coverage`) e falha se exit ≠ 0. Documentado em comment do `vitest.config.mts`.

2. **F17** (TBD commit) `test(backend): cover 7 core FluentValidation validators`
   - Novo `forzion.tech.Tests/Application/Validators/CoreValidatorsTests.cs` (45 tests).
   - Cobertos 7 validators: CadastrarAluno, RegistrarAluno, Login, RegistrarTreinador, CriarTreino, CriarPacote, AtualizarPerfil.
   - Pattern: por rule, happy + boundary + violação. Boundary específico do email-256 corrigido (250 + "@x.com" = 256 estava no limite, ajustado pra 251 = 257).
   - Diferidos para Fase 4: 8 admin-side validators (GruposMusculares, HealthReport-extra, Exercicios/Criar, Pacotes/Atualizar, Planos/*, Treinos/AdicionarExercicio).

**Não feito (reabrir em Fase 4):**
- **F6 MSW migration** — 5 arquivos / 1551 LOC. Pattern `admin.msw.test.ts` já existe como exemplar. Plano: 1 arquivo por sessão, começar pelo menor (`saude/page.client.test.tsx`, 91 LOC).
- **F12 Concurrent billing race** — BLOCKED: Docker offline neste host. Testcontainers exige Docker para Postgres. Reabrir quando Docker Desktop estiver up.
- **F5 provider-side** — Implementar `WithProviderStateUrl` em `ForzionApiProviderTests.cs` + middleware state-aware. Promover `pacts-errors/` → `pacts/` no fim. ~30 min.

**Métricas:**
- Backend: 1160 pass (`Category!=Integration`), era 1115 + 45 novos validator tests.
- Frontend vitest: 380 pass (sem mudança — sem novos vitest tests).
- Frontend Pact: 16 contratos (4 happy + 12 error). 12 error rodam mas não publicam (dir separado).
- PactVerification build: clean. Compilou sem warnings/errors.

**Decisões:**
- F5 provider-side adiado: implementar state handlers no Pact provider exigiria `/_pact/provider-states` endpoint + middleware que detecta state e retorna ProblemDetails. Não-trivial (~30 min de trabalho dedicado). Consumer-side já entrega ~80% do valor (prova que consumer trata 4xx/5xx; documenta shape esperado).
- F8 backend matrix descoberto já implementado em `mutation.yml` (linha 52-54 — matriz hardcoded Domain + Application via `--project`). Static config também atualizado pra documentar.
- F17 cobertura partial é o pragmatic call — os 7 core validators são os críticos (auth + signup + treino + perfil). Admin-side (GruposMusculares etc) menos exposto, segue padrão idêntico, pode ser batch-adicionado em Fase 4 com pouco esforço.
- F6 deferred: scope (1551 LOC) requer ~3 sessões dedicadas. Fase 3 fecha mesmo sem ele.

**Métricas pós-sessão:** Total findings 38 — done 15, deferred 2, blocked 1, pending 20.

**Próximos passos sugeridos (Fase 4):**
- F6 começar pelo menor: `saude/page.client.test.tsx` (mocka `@/lib/api/admin` whole, não client) — migrar pra `server.use(http.get/post("*/admin/health/*"))` interceptors.
- F3 Stripe checkout seed: estratégia de seeded user com pagamento garantido (remove `test.skip()`).
- F7 Stripe partial mock: useStripe/useElements retornam objetos realistas.
- F16 Money rounding CsCheck property test: (amount, tax%, rounding) → banker's rounding + sum preservation.
- F25 Rate limit MAX_ENTRIES eviction.
- F26 PagamentoPix unmount mid-poll.
- F29 Stripe Connect onboarding E2E.
- F30 Cross-aggregate App-level integration.
- F12 reabrir quando Docker Desktop up.

---

## 2026-05-28 — Fase 2 implementada e fechada

**Sessão:** Mesma do dia, continuação após Fase 1.

**Feito (9 commits em `fix/frontend-dockerfile-npmrc`):**

1. **F11** (`552aa3d`) `fix(backend): make Resend webhook idempotent + cover with handler tests`
   - Implementada idempotência por `(ResendMessageId, EventType)` no `ProcessarWebhookResendHandler`.
   - Repo ganhou `ExisteAsync` + `IEmailDeliveryLogRepository`.
   - 11 testes cobrem signature (real via `Svix.Webhook.Sign`), missing secret, payload malformado, 4 event types relevantes, replay silencioso, persistência de fields.

2. **F10** (`9c878ab`) `test(backend,frontend): cover password reset handlers + E2E skeleton`
   - `EsqueceuSenhaHandlerTests` (6 tests): anti-enumeração, token SHA-256 hash 64 hex, expiração 1h via FakeTimeProvider, modo dev sem envio, tokens consecutivos únicos, normalização email.
   - `RedefinirSenhaHandlerTests` (13 tests): token inexistente/expirado/usado, replay (mesmo raw 2x → 2ª falha), conta ausente, validação token + senha forte.
   - `password-reset.spec.ts`: forgot com válido/inexistente → ambos 200 (anti-enum), reset com token inválido → AlertBanner, replay E2E marcado `test.fixme()` aguardando `E2E_RESET_TOKEN_HOOK`.

3. **F9** (`28d6cad`) `test(frontend): prove JwtMiddleware rejects revoked JWT after logout in E2E`
   - Login UI → captura cookie → sanity OK → logout → replay cookie em contexto novo → 401 estrito.

4. **F14** (`e721448`) `test(frontend): add treinador signup E2E covering form + 4xx propagation`
   - 4 specs: senha fraca, senhas diferentes, payload válido → "Solicitação enviada", e-mail duplicado → AlertBanner 4xx.

5. **F15+F23** (`afed78d`) `test(backend,frontend): cover email verification + replay (F23) + resend E2E`
   - `VerificarEmailHandlerTests` (9 tests): inexistente/expirado/já-verificado/replay (F23 explícito)/conta ausente/validação.
   - `email-verification.spec.ts`: token inválido na URL → AlertBanner, sem token → erro visível, resend → anti-enumeração, replay E2E `test.fixme()`.

6. **F19** (`3a4bac6` + `293ff77` + `fa7e9c9`) `test(frontend): cover admin treinador destructive actions with API revert`
   - Specs aprovar/reprovar com cleanup via API. Skip por `E2E_PENDING_TREINADOR_EMAIL`.
   - 2 commits subsequentes pra fix de lint mangling: rule `playwright/prefer-web-first-assertions` reescreve `await x.getAttribute(...)` em expect malformado mesmo com disable comment. Workaround final usa `row.evaluate(el => el.getAttribute(...))`.

**Métricas:**
- Backend: 1154 pass (`Category!=Integration`), era 1115. **+39 tests** novos (11 Resend + 6 Esqueceu + 13 Redefinir + 9 Verificar).
- Frontend vitest: 380 pass (32 suites, sem mudança — tests novos são E2E Playwright).
- E2E specs novos (5): password-reset, logout-revokes-jwt, treinador-signup, email-verification, admin-treinador-crud (updated). Precisam ambiente Playwright completo + seed data pra execução real.

**Decisões:**
- F23 implementado dentro do mesmo commit do F15 — Replay test é o cerne do F23 e cabia no handler test do VerificarEmail.
- F11 ganhou implementação real de idempotência (não só tests) — sem ela os tests só documentariam o gap em vez de fechá-lo.
- E2E destrutivos do F19 + reset/verify replay ficam parcialmente cobertos via unit; E2E full requer endpoints de seed/revert que ainda não existem (TODOs explícitos nos specs com mensagens actionable).
- Lint mangling em F19 forçou workaround via `evaluate()` — `playwright/prefer-web-first-assertions` é agressivo e ignora disable comments após lint-staged re-run.

**Métricas pós-sessão:** Total findings 38 — done 11, deferred 2, pending 25.

**Próximos passos sugeridos (Fase 3):**
- F12 Concurrent billing race: precisa Testcontainers (requer Docker). Spawn 2 tasks paralelas chamando `GerarCobrancaMensal` sobre mesma assinatura. Começar quarantined no CI.
- F4 Pact provider: aplicar pattern repo-level (commit `d7e395e` do ListarAlunos) aos 3 handlers restantes (Fichas/Vinculo/Perfil).
- F5 Pact error contracts: 401/404/500 por endpoint. Frontend test/pact/consumer.test.ts.
- F8 Stryker expansion: incluir `forzion.tech.Application` no backend + `src/components/**` no frontend. Rodar em workflow noturno, não PR gate.
- F6 MSW migration: 5-6 arquivos remanescentes `vi.mock("@/lib/api/*")` pro pattern admin.msw.test.ts.
- F27: confirmar `--check-coverage` no CI; ratchet thresholds.
- F17: ValidatorTests por validator (pattern).

---

## 2026-05-28 — Fase 1 implementada e fechada

**Sessão:** Mesma do setup inicial, continuação.

**Feito (4 commits em `fix/frontend-dockerfile-npmrc`):**

1. **F38** (`c6e8def`) `test(frontend): drop MSW webhook handler defaults from frontend scope`
   - Removidos defaults `POST /webhooks/stripe` e `POST /webhooks/resend` do `frontend/src/test/msw/handlers/pagamento.ts`.
   - Decisão: webhooks são server-to-server, não pertencem ao MSW do frontend. `onUnhandledRequest: "error"` agora pega chamada acidental.
   - Comment inline explica a omissão.

2. **F13** (`0b2f982`) `test(backend): replace Task.Delay(10) with explicit CreatedAt seed`
   - `SeedTreinoAsync` ganhou parâmetro opcional `createdAt`.
   - `ListarPorTreinadorAsync_OrdenarPorCreatedAt_RetornaOrdenadoDesc` agora seeda dois treinos com timestamps 1s apart, eliminando o `Task.Delay(10)` flaky.
   - Teste é `[Trait("Category","Integration")]`, gated por Docker (CI).

3. **F2** (`9c74d35`) `test(frontend): assert offline UI surfaces error banner`
   - Refeito `frontend/e2e/specs/network/offline.spec.ts`. Agora: online → offline+reload → assert AlertBanner com mensagem exata do `usePaginatedList` ("Erro ao carregar alunos.") via `getByRole("alert")` → online+reload → banner some.
   - Eliminado `.catch(() => null)` silencioso. 3 expects estritos no fluxo.

4. **F1** (`46184d1`) `test(frontend): tighten CSRF spec with strict statuses + positive control`
   - 4 testes negativos com `expect(status).toBe(401)` estrito (era `toBeOneOf([401, 403])`).
   - Cobre vetores: sem creds, Cookie forjado, Authorization forjado, POST cross-origin (não só GET).
   - Adicionado positive control via `useAuthRole("admin")`: mesma URL com sessão real recebe status ≠ 401/403 — distingue "defesa funciona" de "endpoint sempre nega".

**Métricas:**
- Backend: 1115 pass (`Category!=Integration`)
- Frontend vitest: 380 pass (32 suites)
- E2E specs offline + CSRF: typecheck ok; precisam de ambiente Playwright completo (auth state + app rodando) pra execução real

**Decisões:**
- F34 e F35 permaneceram `deferred` (infra-scope / platform-limitation).
- Pre-commit hook funcionando bem após `chore(tests)` cb790df (trait-based filter da sessão anterior).
- Fase 2 entra como próxima ativa.

**Métricas pós-sessão:** Total findings 38 — done 4, deferred 2, pending 32.

**Próximos passos sugeridos (Fase 2):**
- F10 Password reset: precisa ler `forzion.tech.Application/UseCases/Auth/*` pra ver se `SolicitarResetSenhaHandler`/`RedefinirSenhaHandler` já existem. Se sim, escrever tests. Se parcial, escrever tests guiando o que falta.
- F11 Resend webhook idempotência: ler `ProcessarWebhookResendHandler.cs` (Infrastructure) e espelhar pattern do Stripe handler test.
- E2E specs (F9, F14, F15, F19) precisam de seed data dedicado — abrir conversa sobre seed strategy antes.

---

## 2026-05-28 — Setup inicial dos arquivos state

**Sessão:** Pós-review completo da suite de testes (após 14 commits de P0s mergeados em `fix/frontend-dockerfile-npmrc` na sessão anterior).

**Feito:**
- Lido plano `~/.claude/plans/fa-a-um-review-completo-virtual-squirrel.md` (38 findings).
- Criado `docs/test-remediation/`:
  - `README.md` — convenções, status, workflow.
  - `state.md` — dashboard inicial com snapshot 0/38 done + baseline de métricas (1115 backend / 380 frontend tests, MSW 4/4, Pact 1/4 repo-level).
  - `findings.md` — catálogo F1-F38 com status `pending` (exceto F34/F35 marcados `deferred` por serem infra-scope ou platform-limitation).
  - `log.md` — este arquivo.

**Decisões importantes:**
- F34 (Pact broker SPOF) e F35 (memory leak Chromium-only) classificados como `deferred` desde já — out of test-suite scope.
- Fase 1 escolhida como ativa (cleanup falso confiança, leverage mais alto).
- Estado guardado em `docs/` (committed) ao invés de `.specs/` (banido pelo AGENTS.md) ou `specs/` (reservado pra specification-*).

**Próximos passos sugeridos:**
- Abrir Fase 1 com F1 (CSRF spec). Leitura inicial: `frontend/e2e/specs/security/csrf.spec.ts`, `frontend/e2e/fixtures/test-base.ts`, `frontend/playwright.config.ts`.

**Não feito:**
- Nenhuma task de remediação iniciada — apenas state files criados.

**Métricas pós-sessão:** Total findings 38 — pending 36, deferred 2, done 0.
