# resultados-load — contenção do request-path & exaustão de pool (fase3, T3)

## O que foi MEDIDO de fato vs RUNBOOK (honestidade)
- **MEDIDO agora (pgbench, executável/determinístico):** curva de contenção das queries
  quentes do request-path (histórico + dashboard/dia) em concorrência crescente, sobre a base
  do seed (300k execuções). Prova o mecanismo de saturação que o cliff de pool amplifica.
- **RUNBOOK (não executado local):** o teste k6 full-app (login + request-path concorrente ao
  lote `/internal/processar-pre-avisos`, com `gate=8` vs `unbounded`). Exige subir a API em duas
  versões de código + contas logáveis (bcrypt) — fora do esforço desta sessão. Scripts prontos:
  `k6-request-path.js`, `k6-batch-trigger.js`, `sample-pg-activity.sh`. Ver § Reproduzir.

## AMBIENTE
PostgreSQL 17-alpine em Docker Desktop/Windows, **16 vCPU locais**, dados quentes, sem RTT de rede.
**Supabase Free (alvo real) = 1–2 vCPU compartilhados + pool pequeno** → o joelho da curva abaixo
chega em concorrência MUITO menor (single-digit), e o cliff é mais abrupto.

## Curva de contenção (pgbench, request-path, índice single-col = estado homolog)
`scripts/perf/load/run-pgbench.sh` — 10s/passo, warm:

| clients | tps | lat média (ms) |
|---|---|---|
| 1 | 1.436 | 0.70 |
| 4 | 5.082 | 0.79 |
| 8 | 8.084 | 0.99 |
| 16 | 10.088 | 1.59 |
| 24 | 11.982 | 2.00 |
| 32 | 12.201 | 2.62 |
| 48 | 12.072 | 3.98 |
| 64 | 12.869 | 4.97 |
| 96 | 12.456 | **7.71** |

**Leitura:** tps escala quase linear até ~núcleos (16) e satura em ~12k. Além da saturação,
**adicionar concorrência NÃO aumenta throughput — só infla latência (0.70 → 7.71 ms, ~11×)**:
as transações extras só enfileiram atrás da CPU saturada. Esse é o mecanismo exato pelo qual um
fan-out concorrente não-limitado degrada o request-path.

## Ligação com o BestEffortConcurrencyGate (achado ALTO da auditoria)
- **homolog (esta branch) = SEM gate:** `DomainEventDispatcher.cs:62` despacha cada evento
  best-effort em `Task.Run` UNBOUNDED. Um lote grande (pré-aviso de milhares de assinaturas)
  gera N tasks DB-bound concorrentes → soma-se ao request-path → empurra a concorrência total
  ALÉM do joelho da curva acima → p95 do request-path infla (starvation de pool).
- **perf/auditoria-performance = COM gate (bound 8):** `BestEffortConcurrencyGate` (commit
  `a486f21`, ainda não em homolog) limita o fan-out a 8 concorrentes → o lote não consome todos
  os slots de pool; o request-path fica do lado esquerdo do joelho.
- **Em Supabase Free** o efeito é dramático: com pool default Npgsql=20 e 1–2 vCPU, o lote
  unbounded satura pool+CPU quase imediato; o gate=8 deixa folga p/ o request-path.

## Veredito
- **CONFIRMADO (mecanismo):** o request-path degrada por enfileiramento assim que a concorrência
  excede a CPU do PG — latência cresce ~11× sem ganho de tps. Quanto menor o tier, mais cedo o joelho.
- **PLAUSÍVEL→exige run full-app p/ número:** que o gate=8 mantém o p95 do request-path estável
  durante o lote enquanto o unbounded o estoura. Os scripts k6 + sampler reproduzem isso quando a
  API estiver no ar; o `sample-pg-activity.sh` flagra `idle in transaction`/`active` subindo no
  cenário unbounded.

## Reproduzir
```bash
# curva pgbench (executável agora, pós scripts/perf/00-setup-db.sh):
scripts/perf/load/run-pgbench.sh

# full-app (runbook): API no ar (Search Path=perf_bench), contas bcrypt-logáveis, X-Internal-Key.
# Terminal A: scripts/perf/load/sample-pg-activity.sh pg-unbounded.csv
# Terminal B: k6 run -e BASE=$API -e ALUNO_EMAIL=.. -e ALUNO_SENHA=.. scripts/perf/load/k6-request-path.js
# Terminal C: k6 run -e BASE=$API -e INTERNAL_KEY=.. scripts/perf/load/k6-batch-trigger.js
# Repetir com a build do gate (perf/auditoria-performance) e comparar p95 + idle-in-tx.
```
