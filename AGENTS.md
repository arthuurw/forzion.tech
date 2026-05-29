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
- `specs/specification-model.md` — modelo tático DDD (entidades, factories, VOs, enums, domain events, máquinas de estado, exceções).
- `specs/specification-backend.md` — backend .NET (camadas Clean Arch, Result, UnitOfWork+dispatch de eventos, validação, DI, middleware/filtros, endpoints, repos, auth/rate-limit).
- `specs/specification-db.md` — estrutura de banco (29 tabelas, migrations, enums, FKs).
- `specs/specification-email.md` — fluxo de envio Resend + templates + webhook.
- `specs/specification-whatsapp.md` — notificações WhatsApp (Meta Cloud API), gate Null/real, handlers/templates, custo per-message, paridade/gaps email→WhatsApp.
- `specs/specification-frontend.md` — Next.js App Router, MUI, formulários, validação.
- `specs/specification-infrastructure.md` — Hostinger VM, docker-compose, nginx, certbot.
- `specs/specification-git.md` — workflow git, configs recomendadas, worktree, conventional commits.
- `specs/specification-stripe.md` — pagamento Stripe Connect + PaymentIntent Pix/Cartão + webhook + billing mensal + CLI.

## CONVENÇÕES-CHAVE
- DDD: entidades com factory `Criar`; domain events despachados no `UnitOfWork.CommitAsync` (re-entrância tratada). Result<T> pattern. FluentValidation auto-descoberto. Handlers registrados manualmente no DI.
- Commits: Conventional Commits. Scopes válidos: `frontend|backend|infra|ci|deps|tests|docs`.

## FLUXO DE ALTERAÇÃO DE CÓDIGO
Toda alteração de código DEVE, antes do PR: (1) build completo (frontend + backend); (2) avaliar necessidade de novos testes e criá-los se faltarem; (3) rodar TODOS os testes — confirmar que nada quebrou e se algo precisa ser complementado (integração/E2E exigem Docker; sem Docker local, o CI os roda no PR); (4) confirmar PR → `homolog` (ou → `master` se a alteração foi feita direto na branch `homolog`).

## REGRAS (só alteráveis mediante aprovação do usuário)
1. CONTEXTO MACRO: carregar APENAS este `AGENTS.md`. Cada `specification-*` (em `specs/`) carregar SOB DEMANDA quando a tarefa tocar a área.
2. Antes de alteração relevante numa área coberta por `specification-*`, LER o arquivo antes de planejar/alterar (ex.: banco → `specs/specification-db.md`).
3. `specification-*` é AGENT-ORIENTED (denso, notação compacta). Criar/alterar: exige revisão (não às cegas), manter atualizado na mesma tarefa, criar SEMPRE em `specs/` (commitado; NUNCA `.specs/`), usar a skill `technical-design-doc-creator` como framework de cobertura (output denso, não TDD verboso).
4. Tarefa com ALTERAÇÃO DE ESCOPO (feature, mudança de comportamento) DEVE usar a skill `tlc-spec-driven` (tasks atômicas + state file). Não se aplica a ajustes triviais.
5. README segue os princípios do `docs-writer` (precisão no código, voz ativa, consistência, verificação de links).
6. Skill citada ausente no projeto: procurar, baixar e instalar antes de usar.
7. PARALELISMO: SEMPRE que a tarefa permitir (análises independentes, implementações em áreas isoladas do código, refatorações que não conflitam, tests em arquivos distintos), delegar pra sub-agents em PARALELO via Agent tool — um sub-agent por escopo isolado, batch num único turno. Coordenação fica no agente principal: ele identifica dependências, lança em paralelo o que não conflita, agrega resultados, faz integração final (DI wiring, commit). Evita serialização desnecessária e mantém context do agente principal limpo (sub-agents devolvem só sumário). Regra subordinada à 4 (tlc-spec-driven define quando paralelizar; "Tasks" phase explicita `[P]` em tasks paralelas).
8. Estas regras só mudam mediante aprovação do usuário.
