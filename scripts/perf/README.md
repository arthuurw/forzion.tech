# scripts/perf — harness de medição de performance (fase 3)

Fecha a lacuna de medição da auditoria de performance: a fase 2 inferiu ganhos por
LEITURA DE CÓDIGO; aqui se PROVA (ou refuta) empiricamente. Spec canônica:
[`specs/specification-load-testing.md`](../../specs/specification-load-testing.md).

> **Schema ISOLADO — nunca dado real.** Tudo roda contra um Postgres efêmero (Docker)
> no schema dedicado `perf_bench`, populado com dados SINTÉTICOS (sem PII). Nunca apontar
> para homolog/develop/public nem para a base de produção.

## Pré-requisitos
- Docker Desktop no ar (Win/Mac/Linux).
- .NET SDK 10 + `dotnet-ef` (`dotnet tool install --global dotnet-ef`).
- `k6` (load test full-app — opcional; `winget install k6.k6`).
- Rodar da RAIZ do repo. Git-bash no Windows: scripts já tratam `MSYS_NO_PATHCONV` onde precisa.

## Fluxo
```bash
# 1) sobe PG efêmero + migra perf_bench + seed sintético (~300k execuções)
scripts/perf/00-setup-db.sh
#    escala: N_TREINADORES=.. N_ALUNOS=.. EXEC_PER_ALUNO=.. scripts/perf/00-setup-db.sh

# 2) EXPLAIN ANALYZE antes/depois do índice composto (aluno_id, data_execucao DESC)
scripts/perf/run-explain.sh scripts/perf/out      # → resultados-explain.md

# 3) curva de contenção do request-path (pgbench)
scripts/perf/load/run-pgbench.sh                  # → resultados-load.md

# 4) Lighthouse + bundle (frontend) — env dummy: API_BASE_URL=x JWT_SECRET=x.. (build prod)
cd frontend && npm run build && npx next experimental-analyze -o   # bundle (analyze é NO-OP sob Turbopack!)
cd frontend && npx next start -p 3100 &            # gzip on; 3000 não auto-migra no Next 16
#    LH público por-URL (sobrevive ao EPERM Windows do lhci) — ver resultados-lighthouse.md § RUNBOOK
#    → resultados-lighthouse.md

# 5) derrubar tudo (remove o container + dados)
scripts/perf/99-teardown.sh
```

## Arquivos
| Arquivo | Papel |
|---|---|
| `00-setup-db.sh` | container efêmero + migrations EF em `perf_bench` + seed |
| `seed-bench.sql` | seed sintético idempotente (md5()::uuid, ON CONFLICT) |
| `explain-targets.sql` / `run-explain.sh` | EXPLAIN das 5 queries-alvo + toggle do índice |
| `load/pgbench-request-path.sql` / `load/run-pgbench.sh` | curva de contenção (executável) |
| `load/k6-request-path.js` / `load/k6-batch-trigger.js` / `load/sample-pg-activity.sh` | load full-app (runbook) |
| `99-teardown.sh` | remove o container |
| `resultados-*.md` | relatórios ANTES/DEPOIS versionados |

## Honestidade dos números
Bench LOCAL (disco rápido, dados quentes, sem RTT) **≠** Supabase Free (vCPU compartilhado,
cache frio, RTT). O sinal durável é o **delta** (plano de query, buffers, forma da curva), não o
milissegundo absoluto. Cada relatório declara seu ambiente.
