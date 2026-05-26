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

## CONVENÇÕES-CHAVE
- DDD: entidades com factory `Criar`; domain events despachados no `UnitOfWork.CommitAsync` (re-entrância tratada). Result<T> pattern. FluentValidation auto-descoberto. Handlers registrados manualmente no DI.
- Commits: Conventional Commits. Scopes válidos: `frontend|backend|infra|ci|deps|tests|docs`.

## REGRAS (só alteráveis mediante aprovação do usuário)
1. CONTEXTO MACRO: carregar APENAS este `AGENTS.md`. Cada `specification-*` (em `specs/`) carregar SOB DEMANDA quando a tarefa tocar a área.
2. Antes de alteração relevante numa área coberta por `specification-*`, LER o arquivo antes de planejar/alterar (ex.: banco → `specs/specification-db.md`).
3. `specification-*` é AGENT-ORIENTED (denso, notação compacta). Criar/alterar: exige revisão (não às cegas), manter atualizado na mesma tarefa, criar SEMPRE em `specs/` (commitado; NUNCA `.specs/`), usar a skill `technical-design-doc-creator` como framework de cobertura (output denso, não TDD verboso).
4. Tarefa com ALTERAÇÃO DE ESCOPO (feature, mudança de comportamento) DEVE usar a skill `tlc-spec-driven` (tasks atômicas + state file). Não se aplica a ajustes triviais.
5. README segue os princípios do `docs-writer` (precisão no código, voz ativa, consistência, verificação de links).
6. Skill citada ausente no projeto: procurar, baixar e instalar antes de usar.
7. Estas regras só mudam mediante aprovação do usuário.
