# AGENTS.md

Guia macro para agentes. Formato agent-oriented (denso, sem prosa decorativa). Para o contexto MACRO do projeto, carregar APENAS este arquivo.

## PROJETO
forzion.tech — SaaS de gestão fitness conectando treinadores e alunos: cadastro/aprovação de treinadores, vínculo de alunos, fichas de treino (exercícios + séries), registro de execuções, assinaturas e pagamentos recorrentes.

## STACK
- Backend: .NET 8 / ASP.NET Core Minimal APIs. Clean Architecture + DDD.
- Persistência: EF Core 8 + PostgreSQL 17 (Supabase). Migrations schema-agnostic (schema via search_path).
- Frontend: Next.js 16 (App Router) + React 19 + MUI + Zod + react-hook-form. Em `frontend/`.
- Auth: JWT + BCrypt. Pagamentos: Stripe (Connect + PaymentIntents/Pix). E-mail: Resend (+ webhook Svix). WhatsApp: Meta Cloud API. Integrações têm impl `Null*` quando não configuradas.
- Testes: backend xUnit + Testcontainers (E2E/Infra exigem Docker); frontend vitest.

## ESTRUTURA
- `forzion.tech.Domain` — entidades, value objects, enums, domain events.
- `forzion.tech.Application` — use cases (handlers), interfaces, Result<T>, validators.
- `forzion.tech.Infrastructure` — EF/repos, integrações, handlers de domain event, seed, migrations.
- `forzion.tech.Api` — endpoints, middleware, DI, Program.cs.
- `forzion.tech.Tests` — testes (xUnit).
- `frontend/` — Next.js.
- `specs/` — docs de referência `specification-*` (versionados/commitados).

## AREAS COBERTAS POR SPECIFICATION-*
Carregar SOB DEMANDA quando tarefa toca a área (regra 2). Índice:
- `specs/specification-model.md` — DDD tático: entidades, factories, VOs, enums, eventos, máquinas de estado, exceções.
- `specs/specification-backend.md` — camadas Clean Arch, Result, UnitOfWork+eventos, validação, DI, middleware/filtros, endpoints, repos, auth/rate-limit.
- `specs/specification-db.md` — schema (29 tabelas, migrations, enums, FKs).
- `specs/specification-email.md` — Resend: envio, templates, webhook (Svix).
- `specs/specification-whatsapp.md` — Meta Cloud API: gate Null/real, handlers/templates, custo, paridade email.
- `specs/specification-frontend.md` — Next App Router, MUI, forms, validação, API proxy, auth.
- `specs/specification-infrastructure.md` — Hostinger VM, docker-compose, nginx, SSL/certbot, CI/CD, env/secrets.
- `specs/specification-git.md` — workflow git, configs, worktree, conventional commits, hooks.
- `specs/specification-lgpd.md` — portabilidade, exclusão por anonimização, consentimento de cookies.
- `specs/specification-tests.md` — disciplina/enforcement, tipos/isolamento, gates (hooks/CI), thresholds, mutation/contract.
- `specs/specification-stripe.md` — Connect + PaymentIntent Pix/Cartão, webhook, billing mensal, CLI.
- `specs/specification-security.md` — threat model, auth, headers/CSP 3 camadas, rate-limit, segredos, SAST/DAST, webhook signing.
- `specs/specification-observability.md` — logging→DB, `/health`, relatório de saúde, RUM (Web Vitals/Sentry), perf budgets.
- `specs/specification-frontend-ui.md` — design tokens, componentes UI/forms, governance, a11y WCAG AA (divergência F18).
- `specs/specification-seo.md` — metadata/canonical, OpenGraph, robots/sitemap, JSON-LD (aspiracional).
- `specs/specification-local-ci-repro.md` — reproduzir os gates do CI localmente + gotchas Windows/Docker (CRLF, MSYS path, coverlet merge, node22/dotnet8, postgres fsync, .slnx, E2E email-block) + achados (Application coverage <85).
- `specs/specification-coding.md` — armadilhas de correção recorrentes (transação↔efeito externo, tempo/auditoria server-side, falhas silenciosas, validação defense-in-depth, codes de erro, dedup). Ler antes de handler/integração/validação/mapeamento de erro.

## CONVENÇÕES-CHAVE
- DDD: entidades com factory `Criar`; domain events despachados no `UnitOfWork.CommitAsync` (re-entrância tratada). Result<T> pattern. FluentValidation auto-descoberto. Handlers registrados manualmente no DI.
- Commits: Conventional Commits. Scopes válidos: `frontend|backend|infra|ci|deps|tests|docs`.

## FLUXO DE ALTERAÇÃO DE CÓDIGO
Toda alteração de código DEVE, antes do PR: (1) build completo (frontend + backend) — `dotnet build forzion.tech.slnx` inclui `forzion.tech.PactVerification` desde 2026-06-06; (2) avaliar necessidade de novos testes e criá-los se faltarem; (3) rodar TODOS os testes — confirmar que nada quebrou e se algo precisa ser complementado (integração/E2E exigem Docker; sem Docker local, o CI os roda no PR); (4) confirmar PR → `homolog` (ou → `master` se a alteração foi feita direto na branch `homolog`).

## ANTES DE QUALQUER OPERAÇÃO GIT/GITHUB
Ler `specs/specification-git.md` SEMPRE antes de commit, push, PR ou qualquer interação com git. Sem exceção. Foco obrigatório em: §PRE-COMMIT HOOK (sequência correta) e §EDGE CASES (CRLF — gotcha recorrente de agents que causa retrabalho: rodar `dotnet format forzion.tech.slnx` ANTES de `git add` em arquivos `.cs` novos).

## REGRAS (só alteráveis mediante aprovação do usuário)
1. CONTEXTO MACRO: carregar APENAS este `AGENTS.md`. Cada `specification-*` (em `specs/`) carregar SOB DEMANDA quando a tarefa tocar a área.
2. Antes de alteração relevante numa área coberta por `specification-*`, LER o arquivo antes de planejar/alterar (ex.: banco → `specs/specification-db.md`).
3. `specification-*` é AGENT-ORIENTED (denso, notação compacta). Criar/alterar: exige revisão (não às cegas), manter atualizado na mesma tarefa, criar SEMPRE em `specs/` (pasta EXCLUSIVA para arquivos `specification-*.md`; commitado), usar a skill `technical-design-doc-creator` como framework de cobertura (output denso, não TDD verboso).
4. Tarefa com ALTERAÇÃO DE ESCOPO (feature, mudança de comportamento) DEVE usar a skill `tlc-spec-driven` (tasks atômicas + state file). Os artefatos de quebra (`spec.md`/`tasks.md`/`STATE.md`) vivem em `.specs/` (gitignored; NÃO em `specs/`, reservada a `specification-*.md`). Não se aplica a ajustes triviais. **PORÉM**: a skill é pra VALIDAR/quebrar escopo NOVO. Se a tarefa é só EXECUTAR algo já quebrado (já existe `tasks.md`/`.specs/` da feature, validado por skill antes), seguir o `tasks.md` direto SEM reinvocar a skill — invocá-la de novo só repete trabalho já validado.
5. README segue os princípios do `docs-writer` (precisão no código, voz ativa, consistência, verificação de links).
6. Skill citada ausente no projeto: procurar, baixar e instalar antes de usar.
7. PARALELISMO: escopos isolados (análises, implementações sem conflito, tests em arquivos distintos) → delegar a sub-agents em PARALELO (Agent tool), 1 por escopo, batch num turno. Principal coordena: identifica dependências, lança o não-conflitante, agrega, integra (DI/commit). Sub-agents devolvem só sumário. Subordinada à regra 4 (`[P]` em tasks paralelas).
8. BUDGET DE CONTEXTO: principal ≤ ~160-170k tokens. Delegar leitura/implementação volumosa a sub-agents (sumário só — regra 7); não reler o que já está em contexto; ler trechos (offset/limit); manter no principal só orquestração/decisão/integração.
9. COMENTÁRIOS: ao escrever qualquer linha de código (produção ou teste), incluir SÓ comentários ESTRITAMENTE necessários — o "porquê" não-óbvio (invariante sutil, workaround com motivo, decisão contraintuitiva, gotcha de plataforma). NÃO comentar o óbvio, NÃO parafrasear o código, NÃO deixar comentário de andaime/referência de tarefa (número de task, "T2B.3:", "TCR1:" etc.). Proibido: section dividers decorativos (`// ── X ──`), inline comments em fim de linha que repetem o que o código diz, multi-line docstrings/XML summary. Preferir nomes claros a comentário. **Esta regra se aplica no momento de escrita — não é revisão pós-fato.** Agentes tendem a over-comentar: antes de apresentar qualquer código ao usuário, passar o olho e remover o ruído.
10. Estas regras só mudam mediante aprovação do usuário.
