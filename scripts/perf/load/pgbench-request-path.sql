-- pgbench custom script: queries do request-path quente do aluno (histórico +
-- dashboard sessões/dia), aluno aleatório por transação. NÃO usa as tabelas do
-- pgbench (-i) — roda direto sobre a base do seed (search_path=perf_bench).
\set n random(1, 5000)
SELECT e.id, e.data_execucao, t.nome
FROM execucoes_treino e
JOIN treinos t ON t.id = e.treino_id
WHERE e.aluno_id = md5('aluno-' || :n)::uuid
ORDER BY e.data_execucao DESC
LIMIT 20;
SELECT date_trunc('day', e.data_execucao, 'UTC') AS dia, count(*)
FROM execucoes_treino e
WHERE e.aluno_id = md5('aluno-' || :n)::uuid
  AND e.data_execucao >= now() - interval '90 days'
  AND e.data_execucao <  now()
GROUP BY date_trunc('day', e.data_execucao, 'UTC');
