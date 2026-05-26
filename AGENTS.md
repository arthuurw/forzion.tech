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
1. CONTEXTO MACRO: carregar APENAS este `AGENTS.md`.
2. Todo arquivo `specification-*` (em `specs/`) contém informação relevante do projeto; carregar em memória/contexto (principal OU subagentes) QUANDO a tarefa exigir.
3. Antes de QUALQUER alteração relevante numa área coberta por um `specification-*`, é OBRIGATÓRIO ler o respectivo arquivo ANTES de planejar ou alterar. Ex.: alteração relevante de banco → ler `specs/specification-db.md` antes de qualquer planejamento ou alteração efetiva.
4. Alterar qualquer `specification-*` exige REVISÃO (não editar às cegas) e mantê-lo AGENT-ORIENTED (denso, notação compacta), como este `AGENTS.md`. Manter atualizado na mesma tarefa em que a estrutura/área correspondente mudar.
5. `specs/` e todos os arquivos dentro dele DEVEM ser commitados (para consultas futuras). `specs/` não é gitignorado (apenas `.specs/` é). Todo novo arquivo `specification-*` DEVE ser criado em `specs/` (NUNCA em `.specs/`, que não é commitada).
6. Criar/alterar qualquer `specification-*` DEVE usar a skill `technical-design-doc-creator` (como framework de cobertura/discovery). Se a skill não estiver instalada no projeto, procurar, baixar e instalar antes de usar. O output deve permanecer AGENT-ORIENTED (denso, notação compacta) conforme regra 4 — a skill guia a cobertura, não o estilo verboso de TDD.
7. OBRIGATÓRIO: toda nova tarefa que envolva ALTERAÇÃO DE ESCOPO (nova feature, mudança de comportamento, expansão de funcionalidade) DEVE ser planejada via skill `tlc-spec-driven` — quebrar em tasks atômicas + manter arquivo de state temporário (e demais fluxos/fases que a própria skill exigir). Se a skill não estiver instalada, procurar, baixar e instalar antes. Não é opcional: garante qualidade no uso da IA (planejamento, rastreabilidade, verificação). Não se aplica a ajustes triviais (typo, config pontual, bugfix isolado).
8. Criar/atualizar qualquer arquivo `README` (raiz, `frontend/`, etc.) DEVE usar a skill `docs-writer`. Se a skill não estiver instalada, procurar, baixar e instalar antes de usar.
9. Estas regras só mudam mediante aprovação do usuário.
