# AGENTS.md

Guia macro para agentes. Formato agent-oriented (denso, sem prosa decorativa). Para o contexto MACRO do projeto, carregar APENAS este arquivo.

## FRESCOR
Validado: 2026-06-13. STACK abaixo é snapshot — se divergir do repo, REPO VENCE: re-detectar e atualizar nesta tarefa. Versões reais: `*.csproj`, `frontend/package.json`.

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
- `forzion.tech.PactVerification` — verificação de contratos Pact (provider). Entra no build da `.slnx`.
- `frontend/` — Next.js.
- `specs/` — docs de referência `specification-*` (versionados/commitados).

## TRIGGER — QUE SPEC CARREGAR (tarefa → co-carregar)
Escreveu CÓDIGO ⇒ SEMPRE +coding +tests. Git ⇒ SEMPRE +git.
| Tocou em… | Carregar |
|---|---|
| handler/use-case/Result/DI         | backend |
| entidade/VO/evento/máquina-estado  | model |
| schema/migration/FK/enum           | db |
| endpoint/contrato/erro API         | backend, security |
| Stripe/refund/webhook pagamento    | stripe, security |
| email/whatsapp                     | email, whatsapp |
| componente/form/página             | frontend, frontend-ui |
| auth/headers/rate-limit/segredo    | security |
| log/health/perf-web                | observability |
| LGPD/consentimento/exclusão        | lgpd |
| CI/hook/gate/cobertura             | tests, local-ci-repro |
| concorrência/lock/idempotência     | concurrency |
| perf backend/N+1/EF/pool           | performance |
| backup/restore/DR                  | dr, infrastructure |
| card/board/pipeline/histórico de entrega | workflow |

## AREAS COBERTAS POR SPECIFICATION-*
Carregar SOB DEMANDA quando a tarefa toca a área (regra 2; TRIGGER acima roteia). Conteúdo de cada spec é auto-evidente pelo nome — abaixo SÓ os caveats não-óbvios (resto: abrir o arquivo):
- `specification-coding.md` — checklist de incidentes; ler ANTES de handler/integração/validação/mapeamento de erro E de QUALQUER code-review.
- `specification-concurrency.md` — ordering: `coding §1` é o canônico.
- `specification-performance.md` — enforcement FRACO (disciplina+revisão, sem gate hard).
- `specification-infrastructure.md` — subir local SEM Docker → §LOCAL-RUN (receita Development+develop; gotcha launch-profile/Homolog; `.env.local` do frontend).
- `specification-frontend-ui.md` — a11y WCAG AA com divergência documentada (F18).
- `specification-seo.md` — ASPIRACIONAL (não implementado).
- `specification-dr.md` — boa parte ALVO/aspiracional, não o estado real.
- `specification-local-ci-repro.md` — reproduzir gates do CI local + gotchas Windows/Docker (CRLF, MSYS path, coverlet merge, node22/dotnet8, postgres fsync, .slnx, E2E email-block).
- `specification-workflow.md` — fluxo de entrega: pipeline de cards (GitHub Projects v2, `.specs` local = fonte de verdade, board = espelho mantido pelo agente via `gh`), Fluxo A (card novo forward) + Fluxo B (backfill histórico→Done consultável por data). Board MONTADO (Project nº1, 37 issues backfill #94-#130; IDs de campo pinados em `§9`); card = issue real com conteúdo completo embutido (durabilidade: `.specs` é gitignored). Manutenção forward via Fluxo A. PENDENTE: automação Action PR-event→Status; tornar `to-issues.sh` idempotente.
- Sem caveat especial (rotear por TRIGGER): `model`, `backend`, `db`, `email`, `whatsapp`, `frontend`, `git`, `lgpd`, `tests`, `stripe`, `security`, `observability`.

## CONVENÇÕES-CHAVE
- DDD: entidades com factory `Criar`; domain events despachados no `UnitOfWork.CommitAsync` (re-entrância tratada). Result<T> pattern. FluentValidation auto-descoberto. Handlers registrados manualmente no DI.
- Commits: Conventional Commits. Scopes válidos: `frontend|backend|infra|ci|deps|tests|docs`.
- Tasks da skill (`tlc-spec-driven`): toda task que escreve código leva no "Done when" o gate de COMENTÁRIOS (regra 9).

## DEFINITION OF DONE (toda alteração de código)
1. Testes: comportamento novo/bugfix tem teste que assevera o COMPORTAMENTO, escrito JUNTO da implementação. NÃO rodar ciclo cerimonial red→green→refactor por iteração — agente escreve código+teste juntos; o valor "falharia sem o código" sai por design. Asserção real, não tautologia (`specification-tests` §11).
2. Build completo: `dotnet build forzion.tech.slnx` (inclui `forzion.tech.PactVerification`) + frontend build.
3. Suíte verde local (unit + vitest; integração/E2E no CI se sem Docker). Contagem NÃO regride; GREEN intocável (`specification-tests` §1.3/§4).
4. Comportamento verificado DE FATO, não só teste verde. [`/verify` · `superpowers:verification-before-completion`]
5. Self-review do diff contra `specification-coding` (checklist de incidentes) ANTES de pedir review. [`superpowers:requesting-code-review`]
6. Comentários: só o "porquê" não-óbvio (regra 9).
7. Git: LER `specs/specification-git.md` ANTES de qualquer op git (CANÔNICO — §PRE-COMMIT HOOK + §EDGE CASES/CRLF). Conventional; `dotnet format forzion.tech.slnx` ANTES de `git add` em `.cs` novos.
8. Commit + push para a BRANCH DE TRABALHO ATUAL apenas. NÃO abrir PR automaticamente — PR (→ `homolog`/`master`) é SEMPRE solicitado manualmente pelo usuário, até segunda ordem (minutos GH Actions limitados).
9. PR (quando o usuário pedir o PR — DoD#8): (a) PRÉ-PR: RE-LER a `specification-*` de CADA área tocada e ATUALIZAR o arquivo na MESMA branch se o código divergiu (specs versionadas = estado REAL, não aspiracional); (b) PÓS-PR, AO CI FICAR VERDE: CODE REVIEW do diff alinhado ao plugin **context7** — toda afirmação que dependa de API de lib/framework (EF Core, Npgsql, Stripe.net, ASP.NET, Serilog, Next.js/React/MUI/Sentry, aws-cli/age/pg_dump) é VERIFICADA no context7 (`resolve-library-id`→`query-docs`) ANTES de confirmar/refutar achado — não alucinar API. Achado cross-file: ler o arquivo real (`coding §7`). O review TAMBÉM valida COMENTÁRIOS DESNECESSÁRIOS (regra 9 / `specification-coding` §8): paráfrase, óbvio, inline que repete o código — o subset que o hook de pre-commit NÃO pega (exige julgamento); flagar pra remoção. CI VERMELHO ⇒ `superpowers:systematic-debugging` da causa PRIMEIRO; NÃO revisar código que ainda falha gate. Fluxo operacional (monitorar checks, ordem): `specification-git` §PUSH/PR.
BUG no caminho ⇒ `superpowers:systematic-debugging`: achar a causa, não remendar sintoma; teste vermelho FICA até o código corrigir.

## REGRAS (só alteráveis mediante aprovação do usuário)
### SISTEMA DE SPECS (como o sistema de specs funciona)
1. CONTEXTO MACRO: carregar APENAS este `AGENTS.md`. Cada `specification-*` (em `specs/`) carregar SOB DEMANDA quando a tarefa tocar a área.
2. Antes de alteração relevante numa área coberta por `specification-*`, LER o arquivo antes de planejar/alterar (ex.: banco → `specs/specification-db.md`).
3. `specification-*` é AGENT-ORIENTED (denso, notação compacta). Criar/alterar: exige revisão (não às cegas), manter atualizado na mesma tarefa, criar SEMPRE em `specs/` (pasta EXCLUSIVA para arquivos `specification-*.md`; commitado), usar a skill `technical-design-doc-creator` como framework de cobertura (output denso, não TDD verboso).
### EXECUÇÃO (como trabalhar)
4. Tarefa com ALTERAÇÃO DE ESCOPO (feature, mudança de comportamento) DEVE usar a skill `tlc-spec-driven` (tasks atômicas + state file). Os artefatos de quebra (`spec.md`/`tasks.md`/`STATE.md`) vivem em `.specs/` (gitignored; NÃO em `specs/`, reservada a `specification-*.md`). Não se aplica a ajustes triviais. **PORÉM**: a skill é pra VALIDAR/quebrar escopo NOVO. Se a tarefa é só EXECUTAR algo já quebrado (já existe `tasks.md`/`.specs/` da feature, validado por skill antes), seguir o `tasks.md` direto SEM reinvocar a skill — invocá-la de novo só repete trabalho já validado.
5. README segue os princípios do `docs-writer` (precisão no código, voz ativa, consistência, verificação de links).
6. Skill citada ausente no projeto: procurar, baixar e instalar antes de usar.
7. PARALELISMO: escopos isolados (análises, implementações sem conflito, tests em arquivos distintos) → delegar a sub-agents em PARALELO (Agent tool), 1 por escopo, batch num turno. Principal coordena: identifica dependências, lança o não-conflitante, agrega, integra (DI/commit). Sub-agents devolvem só sumário. Subordinada à regra 4 (`[P]` em tasks paralelas). Skill obrigatória para dispatch de sub-agents: `superpowers:subagent-driven-development`. Adendo: subagent em worktree pode ter Bash/shell NEGADO ⇒ NÃO roda gates. Principal SEMPRE re-verifica build+testes+format ao integrar — sumário do subagent não é prova.
8. BUDGET DE CONTEXTO: principal ≤ ~160-170k tokens. Delegar leitura/implementação volumosa a sub-agents (sumário só — regra 7); não reler o que já está em contexto; ler trechos (offset/limit); manter no principal só orquestração/decisão/integração.
9. COMENTÁRIOS: só o "porquê" não-óbvio (invariante, workaround, gotcha) — NÃO o óbvio, NÃO parafrasear, NÃO inline que repete o código. Regra de ESCRITA (remover ruído ANTES de apresentar), não revisão pós-fato; agentes over-comentam. Subset barrado por hook + convenções do repo (divisor ASCII só em teste, XML doc só em contrato público): `specs/specification-coding.md` §8.
10. DECISÃO POR 3 EIXOS: toda decisão de design/arquitetura/trade-off (mecanismo, lib, fluxo, parâmetro) pondera EXPLICITAMENTE **segurança + performance + usabilidade** — os três, não um às cegas. Para a escolha "óbvia" do usuário, VALIDAR antes de aceitar (ex.: e-mail-OTP pedido ⇒ apontar que viola segurança, recomendar TOTP). Gray area entre eixos ⇒ expor o trade-off ao usuário (AskUserQuestion) com recomendação fundamentada (fontes/context7 quando depender de API ou postura de segurança — não alucinar). NÃO "andar pra trás" em nenhum eixo sem trade-off consciente e registrado (spec/STATE). Decisões de segurança/postura: ancorar em fonte autoritativa (OWASP/docs oficiais), não em intuição.
11. Estas regras só mudam mediante aprovação do usuário.
