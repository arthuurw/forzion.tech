# Test Suite Remediation — State Tracker

State files persistentes para acompanhar a remediação dos 38 findings do review completo da suite de testes (backend xUnit + Pact + frontend vitest + Playwright + Stryker).

## Files

| Arquivo | Propósito | Atualizado |
|---------|-----------|------------|
| `state.md` | Dashboard alto-nível: contagem por status, fase ativa, métricas | A cada sessão de trabalho |
| `findings.md` | Catálogo completo F1-F38: status + commit SHA + data + notas por finding | A cada finding fechado/movido |
| `log.md` | Log append-only: o que cada sessão fez | A cada sessão (uma entrada por dia/sessão) |
| `phases/phase-N.md` | Tasks atômicas detalhadas por fase, com critérios de aceite | A cada task fechada |

## Status convention

| Status | Significado |
|--------|-------------|
| `pending` | Não iniciado |
| `in_progress` | Em andamento (cite branch) |
| `done` | Implementado + merged em `homolog`/central — cite commit SHA |
| `deferred` | Postergado conscientemente — cite motivo + nova fase ou backlog |
| `skipped` | Descartado após reavaliação — cite motivo |
| `blocked` | Bloqueado por dependência externa — cite o que bloqueia |

## How to use

**No início de cada sessão:**
1. Leia `state.md` pra ver onde está
2. Leia `findings.md` pra confirmar status real (commits e datas)
3. Leia última entrada de `log.md` pra contexto da sessão anterior
4. Escolha próxima task da fase ativa em `phases/phase-N.md`

**Durante a sessão:**
- Marque finding como `in_progress` em `findings.md` ao começar
- Commit traz o SHA — registre em `findings.md` ao fechar
- Adicione obstáculos/decisões em `log.md`

**No fim da sessão:**
- Atualize contadores em `state.md`
- Adicione entrada em `log.md` com: data, findings tocados, decisões importantes, próximos passos
- Se fase concluída, atualize "Fase ativa" em `state.md`

## Source of truth

Plano original (read-only): `C:\Users\arthu\.claude\plans\fa-a-um-review-completo-virtual-squirrel.md`. Não duplicar conteúdo aqui — esses files trackeam **execução**, não **planejamento**.

Findings IDs (F1-F38) são imutáveis. Não renumere mesmo se descartar — use `skipped` em vez.
