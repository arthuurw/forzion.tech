# State

**Last Updated:** 2026-05-24
**Current Work:** Backend Test Harness — Fase 0 ✅ (PR #39 aberto → `backend`); Fases 1–12 pendentes (`docs/backend-harness-tasks.md`)

---

## Recent Decisions (Last 60 days)

### AD-001: Extrair Stripe Connect de Treinador → ContaRecebimento (2026-05-24)
**Decision:** Estado de recebimento (StripeConnectAccountId, OnboardingCompleto) sai de `Treinador` para nova entidade `ContaRecebimento` no contexto Billing.
**Reason:** Vazamento de linguagem de billing no core de Coaching (análise DDD).
**Trade-off:** +1 entidade + lookup por TreinadorId nos handlers.
**Impact:** Onboarding/cobrança/webhook leem via `ContaRecebimento`. Migration data-preserving. Commit `d55aff9`.

### AD-002: GrupoMuscular vira entidade fonte da verdade (FK) (2026-05-24)
**Decision:** `Exercicio` referencia `GrupoMuscular` por `GrupoMuscularId` (FK) em vez do enum `TipoGrupoMuscular`. Enum mantido só como rótulo de seed.
**Reason:** Ambiguidade enum-vs-entidade; grupos editáveis por admin.
**Trade-off:** Migration de backfill (nome→id); FK Restrict.
**Impact:** Exclusão de grupo em uso → 422 (não 500). Resposta expõe GrupoMuscularId + nome. Commit `e879e46`.

### AD-003: Rename plano/pacote/assinatura → plataforma/aluno (2026-05-24)
**Decision:** `PlanoTreinador`→`PlanoPlataforma`, `PacoteAluno`→`Pacote`, `Assinatura`→`AssinaturaAluno`. Full-stack + Pact.
**Reason:** Desambiguar B2B (plataforma) vs B2C (aluno).
**Trade-off:** 173 arquivos; mudança de contrato JSON.
**Impact:** Rotas inalteradas; `planoId`/`pacoteId` mantidos; `assinaturaId`→`assinaturaAlunoId`; request `pacoteAlunoId`→`pacoteId`; response `pacoteAlunoId`→`pacoteId`, `planoTreinadorId`→`planoPlataformaId`. Migration = rename puro. Commit `b2e0802`. Mergeado em homolog (`9fbe7f1`), CI verde incl. Pact provider.

### AD-004: Harness backend — roadmap full-parity 13 fases (2026-05-24)
**Decision:** Plano faseado (0–12) espelhando o harness frontend; determinismo incluído (Fase 1 via `TimeProvider` do .NET 8).
**Reason:** Backend sem harness estruturado; trazer paridade de qualidade.
**Trade-off:** Esforço grande, faseado.
**Impact:** `docs/backend-harness-plan.md` (spec) + `docs/backend-harness-tasks.md` (quebra atômica + guard rails). Commit do plan: `9be698a`.

### AD-005: Fase 0 — baseline de tooling (2026-05-24)
**Decision:** `Directory.Build.props` central; `WarningsAsErrors=nullable` (faseado); gate `dotnet format --verify-no-changes` no CI; `.gitattributes *.cs eol=crlf`.
**Reason:** Qualidade verificável + EOL determinístico cross-platform.
**Trade-off:** WarningsAsErrors só nullable por ora (resto warning).
**Impact:** Commit `a27a1d9`, PR #39 → `backend`.

---

## Active Blockers

### B-001: Testes Docker exigem Docker local
**Discovered:** 2026-05-24
**Impact:** 91 testes (Infrastructure.Repositories + Integration + Notifications/Testcontainers) não rodam sem Docker; gate **quick** os exclui.
**Workaround:** Filtro quick (não-Docker) no dev sem Docker; gate **full** com Docker.
**Resolution:** Rodaram verdes quando Docker subiu (91/91; 1003 total). CI tem serviço Docker.

### B-002: Pact provider verification só em push homolog
**Discovered:** 2026-05-24
**Impact:** `pact-provider.yml` dispara em push `homolog` (broker), não no PR.
**Workaround:** Validar pós-merge.
**Resolution:** Validado em `9fbe7f1` (verde). Considerar rodar no PR (deferred).

---

## Lessons Learned

### L-001: Agentes paralelos em rename divergem no contrato
**Context:** Fase #2 (rename) paralelizada — agente backend ∥ agente frontend.
**Problem:** Backend renomeou propriedades → JSON keys mudaram (`planoTreinadorId`→`planoPlataformaId`, response `pacoteAlunoId`→`pacoteId`), mas o agente frontend manteve os nomes antigos. Pact provider pegaria.
**Solution:** Reconciliação manual pós-execução (grep + fix em 11 arquivos frontend).
**Prevents:** Antes de paralelizar rename cross-stack, travar o contrato JSON EXATO e verificar DTOs reais do backend contra os tipos do frontend.

### L-002: Gate dotnet format quebraria no CI Linux (EOL)
**Context:** Fase 0, adicionar `dotnet format --verify-no-changes`.
**Problem:** Repo guarda `.cs` como LF (`* text=auto`); `.editorconfig` pede CRLF → checkout LF no Linux falharia o verify (passava só no Windows via autocrlf).
**Solution:** `.gitattributes` `*.cs text eol=crlf` (checkout CRLF em todas plataformas).
**Prevents:** Alinhar `.gitattributes` ↔ `.editorconfig` antes de gate de formatação cross-platform.

### L-003: Sonar S1135 falso-positivo na palavra PT "todo"
**Context:** Build com warnings visíveis (Fase 0).
**Problem:** `S1135` flagou "todo grupo" (PT = "every group") como TODO tag, em migration gerada.
**Solution:** `[**/Migrations/**.cs] dotnet_analyzer_diagnostic.severity = none` no `.editorconfig`.
**Prevents:** Tratar migrations como generated_code (exemptas de analyzers).

### L-004: commitlint rejeita scope/subject fora do padrão
**Context:** Commits de fase.
**Problem:** scope-enum restrito a `[frontend,backend,infra,ci,deps,tests,docs]`; subject não pode ser sentence-case.
**Solution:** Usar scope válido + subject minúsculo.
**Prevents:** Falha de commit-msg hook.

---

## Quick Tasks Completed

| #   | Description | Date | Commit | Status |
| --- | ----------- | ---- | ------ | ------ |
| 001 | DDD #1 Stripe→ContaRecebimento | 2026-05-24 | `d55aff9` | ✅ |
| 002 | DDD #3 GrupoMuscular FK | 2026-05-24 | `e879e46` | ✅ |
| 003 | DDD #2 rename plataforma/aluno | 2026-05-24 | `b2e0802` | ✅ |
| 004 | docs backend-harness-plan | 2026-05-24 | `9be698a` | ✅ |
| 005 | Harness Fase 0 (tooling baseline) | 2026-05-24 | `a27a1d9` | ✅ (PR #39) |

---

## Deferred Ideas

- [ ] Regenerar OpenAPI types do frontend (`frontend/src/test/msw/types.ts`) — ainda lista shape antigo de exercício (grupoMuscular enum). — Captured during: DDD #3 GrupoMuscular.
- [ ] Pact provider verification rodar também no PR (hoje só push homolog). — Captured during: validação CI.
- [ ] F1.5 IGuidProvider e F12.2 flaky detection são opcionais. — Captured during: harness tasks.

---

## Todos

- [ ] Decidir merge do PR #39 (Fase 0 → `backend`).
- [ ] Commitar `docs/backend-harness-tasks.md` (criado, ainda não commitado).
- [ ] `docs/pact-broker-homolog.md`: deleção feita pelo usuário, deixada fora dos commits do assistente — usuário commita quando quiser.
- [ ] Antes de F4/F5: confirmar ferramentas (builder à mão vs AutoFixture/Bogus; CsCheck vs FsCheck) e validar APIs via context7.
- [ ] Fase 1 toca o Domain (~21 entidades) — revisar escopo com cuidado; suíte verde por grupo.

---

## Preferences

**Model Guidance Shown:** never
**Workflow:** 1 branch + 1 PR por fase → base `backend`; Conventional Commits scope `backend`; não spawnar subagents sem pedido explícito (exceto plan mode / quando solicitado); CAVEMAN mode ativo nesta sessão.
