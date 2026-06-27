# resultados-explain — EXPLAIN ANALYZE antes/depois (perf-measurement-harness-fase3, T2)

Prova empírica dos achados de índice da auditoria de performance. Gerado por
`scripts/perf/run-explain.sh` sobre a base de `scripts/perf/seed-bench.sql`
(5.000 alunos, 300.000 `execucoes_treino`, 300.000 `execucoes_exercicio`).

**Toggle:** ANTES = índice single-col `ix_execucoes_treino_aluno_id (aluno_id)`
(estado de `homolog`). DEPOIS = índice composto
`ix_execucoes_treino_aluno_id_data_execucao (aluno_id, data_execucao DESC)`
(quick-win da `perf/auditoria-performance`, ainda não mergeado em homolog).

## AMBIENTE (honestidade — bench local ≠ produção)
- PostgreSQL 17-alpine em Docker Desktop/Windows, container efêmero, disco local rápido.
- Dados QUENTES (todos os planos abaixo: `shared hit`, ~0 `read`) → tempos absolutos são best-case.
- **Supabase Free (alvo real)** = shared CPU + cache frio + RTT de rede: o tempo ABSOLUTO será
  maior e mais ruidoso. O sinal DURÁVEL aqui é o **delta de PLANO** (sort eliminado, index-only,
  redução de buffers/heap) — não os milissegundos.

## Veredito por query
| # | Query (repo) | Achado auditoria | ANTES | DEPOIS | Veredito |
|---|---|---|---|---|---|
| Q1 | histórico `ListarComNomePorAlunoAsync` | índice composto evita sort | Bitmap Index Scan (single-col) → **top-N heapsort** dos 60; 243 buf; 2.14 ms | Index Scan no composto, **sem sort**, LIMIT corta em 20; 80 buf; 0.26 ms | **CONFIRMADO** — sort eliminado, ~8× tempo, 3× buffers |
| Q2 | dashboard `ContarSessoesPorDiaAsync` | índice cobre filtro+range | Bitmap Index Scan + Filter (remove 46/60) + Sort; 63 buf; 0.97 ms | **Index Only Scan** (range no Index Cond, Heap Fetches=0); 4 buf; 0.19 ms | **CONFIRMADO (forte)** — covering, 16× menos buffers, ~5× tempo |
| Q3 | progressão `ProjetarProgressaoAsync` | índice ajuda o scan-base | Bitmap (60 → filtra 46); 220 buf; 0.50 ms | range no índice (lê só 14); 178 buf; 0.55 ms | **PARCIAL** — scan-base de 63→17 buf, mas a cadeia de joins domina; ganho marginal |
| Q4 | vínculo `ListarComDetalhesAsync` / `TemVinculoAtivoPrevio` | over-scan da subquery | **Seq Scan** em `vinculos` (DISTINCT, 5000) + Seq Scan em `alunos`; 3.72 ms | idêntico (índice composto não toca esta tabela); 3.20 ms | **CONFIRMADO (achado distinto)** — composto não ajuda; a subquery `DISTINCT` faz Seq Scan full. Otimização = LOTE SEPARArDO |
| Q5 | admin `ListarTodosAsync` (nome ILIKE) | sem trigram → Seq Scan | **Seq Scan** em `alunos`, varre 5000, remove 4999; 1.81 ms | idêntico (btree não serve a `ILIKE '%...%'`); 1.81 ms | **CONFIRMADO** — precisa `pg_trgm` GIN; linear no nº de alunos. Fora deste lote (não-objetivo) |

## Conclusões
1. **Índice composto `(aluno_id, data_execucao DESC)` é JUSTIFICADO** pelos planos: elimina o
   top-N heapsort do histórico (Q1) e habilita Index-Only Scan no dashboard (Q2, achado mais forte —
   16× menos buffers). O quick-win da `perf/auditoria-performance` é real; promovê-lo a homolog procede.
2. **Q3 progressão** ganha pouco do índice — o custo está na cadeia de joins (`execucoes_exercicio`
   → `treino_exercicios` → `exercicios` → `grupos`), não no scan-base. Não é alvo do índice composto.
3. **Q4 e Q5 são achados REAIS mas de OUTRO lote**: o `Seq Scan` da subquery `TemVinculoAtivoPrevio`
   (Q4) e o `ILIKE '%...%'` sem trigram (Q5) NÃO se resolvem com o índice composto. Ambos foram
   listados como BAIXO/lote-separado na `spec` da fase 2 — esta medição os RE-CONFIRMA empiricamente.

## Reproduzir
```bash
# 1. subir DB + migrar + seed → scripts/perf/README.md
# 2. toggle + captura:
scripts/perf/run-explain.sh scripts/perf/out
# saídas cruas: scripts/perf/out/explain-antes.txt | explain-depois.txt
```
