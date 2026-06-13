# specification-workflow — fluxo de entrega (board + histórico)

DOC PARA AGENTES. Fonte de verdade do fluxo de desenvolvimento: pipeline de cards (backlog→done), ferramenta (GitHub Projects v2), os 2 fluxos (card novo forward + backfill histórico), responsabilidade de sincronização e coexistência com AGENTS.md/`.specs`/`STATE.md`. Formato denso. Cross-ref: AGENTS.md (regras 4/8 — tlc-spec-driven, no-auto-PR), [specification-git] (PUSH/PR), STATE.md (memória de decisões).

## MANUTENÇÃO DESTE ARQUIVO
Atualizar quando mudar: estados do pipeline ou suas transições/gates, schema de campos do Project, divisão de responsabilidade de sync (quem atualiza o card e quando), mecânica do backfill, ou a ferramenta. NÃO duplicar a mecânica de git de [specification-git] — REFERENCIAR.

## 1. OBJETIVO / PRINCÍPIO
Fluxo de entrega profissional (visibilidade de backlog → em dev → testado → aguardando PR → done) SEM perder a velocidade do `.specs` local. Invariante central:
- **`.specs/` local = autoria + fonte de verdade** (rápido, em contexto, `tlc-spec-driven`).
- **GitHub Project = espelho de rastreamento**, mantido pelo AGENTE (não editado à mão pelo usuário). Usuário abre o card de 1 linha; o agente sincroniza o resto.
Resultado: fluidez preservada (1-liner humano) + pipeline visível + histórico permanente consultável por data.

## 2. FERRAMENTA
**GitHub Projects v2** (no mesmo repo/owner). Escolha registrada (2026-06-13) — análise em [[mfa-sessao-specs-pending]] thread: grátis SEM cap de issues (Linear free = 250, fatal p/ backfill), PR-nativo (transição aguardando-PR→done via Actions já existentes), já no stack.
- **Driver primário: `gh` CLI** (já autenticado) — `gh project create`, `gh project field-create`, `gh project item-create`/`item-add`, `gh project item-edit` (status via `--single-select-option-id`); mutações de campo que o CLI não cobrir → GraphQL (`gh api graphql`). Sintaxe exata dos flags é pinada no setup (não assumir às cegas — verificar `gh project --help` na criação).
- **MCP opcional**: GitHub MCP server ganhou toolset de Projects (out/2025), mas mutação de status-field é parcial — `gh`/GraphQL é o caminho completo. MCP não é pré-requisito.
- **Tipo de card — HÍBRIDO** (decidido no setup 2026-06-13):
  - **Backfill histórico (Fluxo B) = draft item** do Project. Razão: 37 itens já fechados não devem poluir o issue-tracker do repo (ruído vs. bugs reais), zero notificação, zero write de issue. Os campos dedicados **Concluído em** (date) + **Tipo**/**Área** (single-select) cobrem data/label que justificariam issue. Consulta por data funciona igual.
  - **Card novo (Fluxo A, forward) = GitHub Issue real.** Razão: a automação de transição (PR-open→In Review, merge→Done) precisa de issue real p/ o GitHub linkar PR/commit nativamente — draft não linka PR. Trade-off consciente (AGENTS.md regra 10): draft é ótimo p/ histórico imutável, issue é necessário onde há PR a rastrear.

## 3. PIPELINE — ESTADOS (campo Status, single-select)
Ordem e gate de ENTRADA de cada estado (o gate é o que precisa estar verdadeiro p/ entrar):

| # | Status | Entra quando | Artefato .specs |
|---|---|---|---|
| 1 | **Backlog** | ideia registrada, ainda não priorizada | — (ou nota STATE) |
| 2 | **Specifying** | priorizado; escrevendo requisitos | `spec.md` em progresso |
| 3 | **Designing** | spec fechada; desenhando arquitetura | `design.md` (+`style.md` se UI) |
| 4 | **Ready for Dev** | tasks atômicas quebradas e validadas | `tasks.md` pronto |
| 5 | **In Dev** | executando tasks | tasks em progresso |
| 6 | **Testing/Verify** | código + testes escritos; rodando gates + `/verify` | suíte verde local |
| 7 | **Developed (Awaiting PR)** | comportamento verificado, push na branch de trabalho; **PR ainda NÃO aberto** (AGENTS.md regra 8) | commit refs |
| 8 | **In Review** | PR aberto; CI rodando/code-review | PR ref |
| 9 | **Done** | PR mergeado (→ homolog/master) | PR mergeado + data |
| — | **Deferred** | adiado/parado (saiu do fluxo ativo) | nota STATE |

Estados pequenos (2–4) podem ser pulados em escopo Small/Medium (auto-sizing do `tlc-spec-driven`): nesse caso o card salta direto p/ o estado relevante. O pipeline NÃO força cerimônia onde a feature não pede.

## 4. SCHEMA DO CARD (Issue + campos do Project)
- **Title**: `<área>: <feature>` (ex.: `backend: MFA híbrido TOTP+email`).
- **Status**: §3.
- **Área** (single-select): backend|frontend|infra|ci|deps|tests|docs (= scopes válidos de commit, AGENTS.md).
- **Specs path** (text): caminho do `.specs/features/<slug>/` (ou pasta local externa, no backfill).
- **Refs** (no corpo/comentário): commits + PR (link nativo do GitHub).
- **Datas**: criação (auto) + fechamento (auto no merge→Done) — base da consulta histórica.
- **Labels**: `feature`|`fix`|`chore`|`spec`; + área (redundância p/ filtro rápido).

## 5. FLUXO A — CARD NOVO (forward)
1. **Usuário** abre issue de 1 linha (título + intenção). Status=Backlog/Specifying.
2. **Agente** roda `tlc-spec-driven`, escreve `.specs` LOCAL (autoria rápida — inalterado).
3. **Agente** sincroniza o card a CADA fase concluída (não a cada edição): move Status (Specifying→Designing→Ready for Dev→In Dev→Testing→Awaiting PR), cola sumário curto + path no card via `gh`. Granularidade = fase, não keystroke (preserva velocidade).
4. Push na branch → Status=**Developed (Awaiting PR)**. PR **NÃO** automático (regra 8).
5. **Usuário** pede/abre PR → Status=**In Review** (pode ser automatizado por Action de PR-open).
6. Merge → Status=**Done**, data de fechamento registrada (Action de merge ou `gh` no fechamento).
Responsabilidade: passos 2–4 = agente; 1/5 = humano (1-liner + decisão de PR). `STATE.md` continua p/ decisões/blockers entre sessões; o card é o status público.

## 6. FLUXO B — BACKFILL (histórico → Done)
Objetivo: toda `.specs` já desenvolvida (neste repo + pastas locais externas que o usuário apontar) entra no board em **Done** com data, p/ responder "o que foi entregue em X data?".
1. **Inventário**: varrer cada raiz `.specs` informada; listar features (pastas com `spec.md`/`tasks.md`).
2. **Derivar metadados** por feature: título, área, **data de conclusão** (prioridade: `STATE.md` > `git log` do commit/PR relacionado > mtime do arquivo), sumário (1–2 linhas do `spec.md`), path, refs de commit/PR se rastreáveis.
3. **Criar draft item direto em Done** (`gh project item-create` + `gh project item-edit` set Status=Done + Concluído em + Tipo/Área + Specs path). Sem passar pelo pipeline. (Draft, não issue — §2.)
4. **Idempotência**: marcar por `Specs path` único; re-rodar o backfill NÃO duplica (checar item existente antes de criar).
5. Roda 1x p/ o acervo; depois é append-only conforme novas features fecham pelo Fluxo A.
Caveat: features sem data rastreável → marcar `data ≈ <mtime>` com label `data-aproximada` (não inventar precisão — AGENTS.md regra 10).

## 7. CONSULTA DE HISTÓRICO
"O que foi desenvolvido em X data" = filtro do Project/Issues por Status=Done + intervalo de data de fechamento (+ área/label). Permanente (issues não expiram, sem cap). Exportável (`gh issue list --json`).

## 8. COEXISTÊNCIA COM AGENTS.md / .specs / STATE
- **Não substitui** `.specs` nem `STATE.md`: `.specs` = conteúdo/autoria; `STATE.md` = decisões/blockers/lições entre sessões; **card = status + índice consultável**. Sem duplicar conteúdo no card (sumário + link, não a spec inteira).
- **Regra 4** (tlc-spec-driven) inalterada: o board reflete as fases, não as substitui.
- **Regra 8** (no-auto-PR) reforçada: estado "Developed (Awaiting PR)" é explícito; agente NÃO abre PR sozinho.
- **`.specs` segue gitignored** (regra 4): o board + (opcional) o backfill é que dão durabilidade ao histórico, sem precisar commitar `.specs`.
- Registrar este fluxo no índice do AGENTS.md (TRIGGER + AREAS) — pendente de aprovação do usuário (regra 10).

## 9. SETUP REALIZADO — IDs PINADOS (2026-06-13)
Project criado + backfill de 37 cards feito. Reprodutível via `.specs/features/workflow-board-setup/backfill.sh`.
- **Project**: nº `1`, owner `@me` (arthuurw), id `PVT_kwHOAg7S384Bahuc`, URL https://github.com/users/arthuurw/projects/1
- **Status** (field `PVTSSF_lAHOAg7S384BahuczhVYva0`): Backlog `b6b7700f` · Specifying `a63049f8` · Designing `c21a6838` · Ready for Dev `4c31c39c` · In Dev `a595fcdd` · Testing/Verify `5ce5da2d` · Developed (Awaiting PR) `3d21448b` · In Review `ec4b4c71` · Done `d63a755c` · Deferred `9a60c270`
- **Area** (field `PVTSSF_lAHOAg7S384BahuczhVYvhA`): backend `553ac63b` · frontend `01e6e4e2` · infra `9361f93a` · ci `fb085e02` · stripe `f834d69f` · email `15a118f3` · whatsapp `14618e14` · db `5fb6f308` · security `e95e9f54` · lgpd `3ecad119` · tests `9519b63d` · docs `720568dc` · workflow `fb9f2a57`
- **Tipo** (field `PVTSSF_lAHOAg7S384BahuczhVYvhc`): feature `a4dda951` · fix `a0265df9` · chore `b176c6ff` · spec `f2da1e24`
- **Specs path** (text field `PVTF_lAHOAg7S384BahuczhVYvhg`) · **Concluído em** (date field `PVTF_lAHOAg7S384BahuczhVYvhk`)
- Backfill resultante: 37 cards (22 Done · 1 Developed/Awaiting PR · 3 In Dev · 11 Ready for Dev), zero dup. Idempotência: chave = Specs path; re-rodar deve checar item existente antes de criar (o script atual NÃO checa — re-rodar duplicaria; ajustar antes de re-executar).

### Gaps remanescentes
- Automação de transições por evento de PR (Action `pull_request`/`pull_request_target` → mover Status) — só p/ cards-novos-issue (Fluxo A); fase posterior. Até lá, transição via `gh` pelo agente.
- Raízes `.specs` externas do backfill: o usuário fornece os caminhos (não auto-descobrir o disco inteiro).
