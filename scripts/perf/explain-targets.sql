-- EXPLAIN (ANALYZE, BUFFERS) das queries-alvo da auditoria de performance.
-- search_path = perf_bench (via PGOPTIONS). Roda sobre a base do seed-bench.sql.
-- O runner (run-explain.sh) executa este arquivo 2×: ANTES (índice single-col
-- ix_execucoes_treino_aluno_id) e DEPOIS (composto aluno_id, data_execucao DESC).
-- Aluno/treinador amostra fixos p/ comparabilidade entre execuções.

\set ON_ERROR_STOP on
\timing off

\echo ============================================================
\echo Q1 historico (ListarComNomePorAlunoAsync) — execucoes JOIN treinos, ORDER BY data DESC, LIMIT 20
\echo ============================================================
EXPLAIN (ANALYZE, BUFFERS)
SELECT e.id, e.data_execucao, t.nome
FROM execucoes_treino e
JOIN treinos t ON t.id = e.treino_id
WHERE e.aluno_id = md5('aluno-2500')::uuid
ORDER BY e.data_execucao DESC
LIMIT 20 OFFSET 0;

\echo ============================================================
\echo Q2 dashboard sessoes/dia (ContarSessoesPorDiaAsync) — filtro por aluno + range, GROUP BY dia
\echo ============================================================
EXPLAIN (ANALYZE, BUFFERS)
SELECT date_trunc('day', e.data_execucao, 'UTC') AS dia, count(*)
FROM execucoes_treino e
WHERE e.aluno_id = md5('aluno-2500')::uuid
  AND e.data_execucao >= now() - interval '90 days'
  AND e.data_execucao <  now()
GROUP BY date_trunc('day', e.data_execucao, 'UTC');

\echo ============================================================
\echo Q3 progressao (ProjetarProgressaoAsync) — cadeia de joins + GROUP BY, filtro por aluno + range
\echo ============================================================
EXPLAIN (ANALYZE, BUFFERS)
SELECT ex.nome, gm.nome, date_trunc('day', e.data_execucao, 'UTC') AS dia,
       max(ee.carga_executada),
       avg(ee.series_executadas::float8),
       avg(ee.repeticoes_executadas::float8)
FROM execucoes_treino e
JOIN execucoes_exercicio ee ON ee.execucao_treino_id = e.id
JOIN treino_exercicios te ON te.id = ee.treino_exercicio_id
JOIN exercicios ex ON ex.id = te.exercicio_id
JOIN grupos_musculares gm ON gm.id = ex.grupo_muscular_id
WHERE e.aluno_id = md5('aluno-2500')::uuid
  AND e.data_execucao >= now() - interval '90 days'
  AND e.data_execucao <= now()
GROUP BY ex.nome, gm.nome, date_trunc('day', e.data_execucao, 'UTC')
ORDER BY 2, 1, 3;

\echo ============================================================
\echo Q4 vinculo+TemVinculoAtivoPrevio (ListarComDetalhesAsync) — subquery DISTINCT por treinador
\echo ============================================================
EXPLAIN (ANALYZE, BUFFERS)
SELECT v.id, a.nome,
       (v.aluno_id IN (
          SELECT DISTINCT v2.aluno_id FROM vinculos_treinador_aluno v2
          WHERE v2.status = 'Ativo' AND v2.treinador_id <> md5('treinador-100')::uuid)) AS tem_previo
FROM vinculos_treinador_aluno v
JOIN alunos a ON a.id = v.aluno_id
WHERE v.treinador_id = md5('treinador-100')::uuid
ORDER BY v.created_at DESC
LIMIT 20 OFFSET 0;

\echo ============================================================
\echo Q5 busca admin alunos ILIKE (ListarTodosAsync nome) — sem indice trigram, esperado Seq Scan
\echo ============================================================
EXPLAIN (ANALYZE, BUFFERS)
SELECT a.id, a.nome
FROM alunos a
WHERE a.nome ILIKE '%Bench 1234%'
ORDER BY a.nome
LIMIT 20 OFFSET 0;
