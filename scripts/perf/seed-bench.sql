-- seed sintético para o harness de medição (perf-measurement-harness-fase3).
-- Schema-alvo: definido pelo search_path da sessão (perf_bench). NUNCA rodar em
-- homolog/develop/public — ver scripts/perf/README.md (§ schema isolado).
-- IDs determinísticos (md5(texto)::uuid) → reexecutável; ON CONFLICT DO NOTHING.
-- Escala via psql vars (-v): n_treinadores, n_alunos, exec_per_aluno.

\set ON_ERROR_STOP on
\if :{?n_treinadores}
\else
  \set n_treinadores 200
\endif
\if :{?n_alunos}
\else
  \set n_alunos 5000
\endif
\if :{?exec_per_aluno}
\else
  \set exec_per_aluno 60
\endif

\echo seeding perf_bench :n_treinadores treinadores / :n_alunos alunos / :exec_per_aluno execucoes-por-aluno

BEGIN;

INSERT INTO grupos_musculares (id, nome, created_at)
SELECT md5('grupo-'||g)::uuid, 'Grupo '||g, now()
FROM generate_series(1,8) g
ON CONFLICT (id) DO NOTHING;

INSERT INTO planos_plataforma (id, nome, descricao, preco, max_alunos, tier, is_ativo, created_at)
SELECT md5('plano-'||p)::uuid, 'Plano '||p, 'bench', (p*50)::numeric, p*50, t.tier, true, now()
FROM (VALUES (1,'Free'),(2,'Pro'),(3,'Elite')) AS t(p,tier)
ON CONFLICT (id) DO NOTHING;

INSERT INTO contas (id, email, password_hash, tipo_conta, created_at, email_verificado, verificado_em)
SELECT md5('conta-treinador-'||t)::uuid, 'treinador'||t||'@bench.local',
       '$2a$11$benchbenchbenchbenchbenchbe', 'Treinador', now(), true, now()
FROM generate_series(1,:n_treinadores) t
ON CONFLICT (id) DO NOTHING;

INSERT INTO treinadores (id, conta_id, nome, plano_plataforma_id, status, created_at, modo_pagamento_aluno, anonimizado)
SELECT md5('treinador-'||t)::uuid, md5('conta-treinador-'||t)::uuid,
       'Treinador Bench '||t, md5('plano-'||(((t-1)%3)+1))::uuid,
       'Ativo', now(), 'Plataforma', false
FROM generate_series(1,:n_treinadores) t
ON CONFLICT (id) DO NOTHING;

INSERT INTO pacotes (id, treinador_id, nome, descricao, preco, is_ativo, created_at)
SELECT md5('pacote-'||t)::uuid, md5('treinador-'||t)::uuid, 'Pacote '||t, 'bench', 99.90, true, now()
FROM generate_series(1,:n_treinadores) t
ON CONFLICT (id) DO NOTHING;

INSERT INTO exercicios (id, treinador_id, nome, grupo_muscular_id, created_at)
SELECT md5('exerc-'||t||'-'||e)::uuid, md5('treinador-'||t)::uuid,
       'Exercicio '||t||'-'||e, md5('grupo-'||(((t+e)%8)+1))::uuid, now()
FROM generate_series(1,:n_treinadores) t, generate_series(1,3) e
ON CONFLICT (id) DO NOTHING;

-- treino = ficha owned por 1 aluno (ix_treino_alunos_treino_id é UNIQUE entre
-- treino_alunos Ativo) → treinos são per-aluno, não compartilhados. 3 fichas/aluno.
INSERT INTO treinos (id, treinador_id, nome, objetivo, created_at, dificuldade)
SELECT md5('treino-'||i||'-'||k)::uuid,
       md5('treinador-'||(((i-1)%:n_treinadores)+1))::uuid,
       'Treino '||i||'-'||k, 'Hipertrofia', now(), 'Iniciante'
FROM generate_series(1,:n_alunos) i, generate_series(1,3) k
ON CONFLICT (id) DO NOTHING;

INSERT INTO treino_exercicios (id, treino_id, exercicio_id, ordem)
SELECT md5('te-'||i||'-'||k||'-'||m)::uuid,
       md5('treino-'||i||'-'||k)::uuid,
       md5('exerc-'||(((i-1)%:n_treinadores)+1)||'-'||(((m-1)%3)+1))::uuid, m
FROM generate_series(1,:n_alunos) i, generate_series(1,3) k, generate_series(1,6) m
ON CONFLICT (id) DO NOTHING;

INSERT INTO contas (id, email, password_hash, tipo_conta, created_at, email_verificado, verificado_em)
SELECT md5('conta-aluno-'||i)::uuid, 'aluno'||i||'@bench.local',
       '$2a$11$benchbenchbenchbenchbenchbe', 'Aluno', now(), true, now()
FROM generate_series(1,:n_alunos) i
ON CONFLICT (id) DO NOTHING;

INSERT INTO alunos (id, conta_id, nome, email, status, created_at, anonimizado)
SELECT md5('aluno-'||i)::uuid, md5('conta-aluno-'||i)::uuid,
       'Aluno Bench '||i, 'aluno'||i||'@bench.local', 'Ativo', now(), false
FROM generate_series(1,:n_alunos) i
ON CONFLICT (id) DO NOTHING;

INSERT INTO vinculos_treinador_aluno (id, treinador_id, aluno_id, pacote_id, status, aprovado_em, data_inicio, created_at)
SELECT md5('vinc-'||i)::uuid,
       md5('treinador-'||(((i-1)%:n_treinadores)+1))::uuid,
       md5('aluno-'||i)::uuid,
       md5('pacote-'||(((i-1)%:n_treinadores)+1))::uuid,
       'Ativo', now(), now() - interval '200 days', now() - interval '200 days'
FROM generate_series(1,:n_alunos) i
ON CONFLICT (id) DO NOTHING;

INSERT INTO assinaturas_aluno (id, vinculo_id, pacote_id, treinador_id, aluno_id, valor, status,
       data_inicio, data_proxima_cobranca, created_at, tentativas_falhas_consecutivas)
SELECT md5('assin-'||i)::uuid, md5('vinc-'||i)::uuid,
       md5('pacote-'||(((i-1)%:n_treinadores)+1))::uuid,
       md5('treinador-'||(((i-1)%:n_treinadores)+1))::uuid,
       md5('aluno-'||i)::uuid, 99.90,
       CASE WHEN i % 10 = 0 THEN 'Inadimplente' ELSE 'Ativa' END,
       now() - interval '200 days', now() + interval '20 days', now() - interval '200 days',
       CASE WHEN i % 10 = 0 THEN 2 ELSE 0 END
FROM generate_series(1,:n_alunos) i
ON CONFLICT (id) DO NOTHING;

INSERT INTO pagamentos (id, assinatura_aluno_id, valor, status, data_pagamento, created_at, metodo_pagamento)
SELECT md5('pag-'||i||'-'||p)::uuid, md5('assin-'||i)::uuid, 99.90,
       CASE WHEN p = 1 AND i % 10 = 0 THEN 'Pendente' ELSE 'Pago' END,
       now() - ((p*30)||' days')::interval, now() - ((p*30)||' days')::interval, 'Pix'
FROM generate_series(1,:n_alunos) i, generate_series(1,3) p
ON CONFLICT (id) DO NOTHING;

INSERT INTO treino_alunos (id, treino_id, aluno_id, status, created_at)
SELECT md5('ta-'||i||'-'||k)::uuid,
       md5('treino-'||i||'-'||k)::uuid,
       md5('aluno-'||i)::uuid, 'Ativo', now() - interval '200 days'
FROM generate_series(1,:n_alunos) i, generate_series(1,3) k
ON CONFLICT (id) DO NOTHING;

INSERT INTO execucoes_treino (id, treino_id, aluno_id, data_execucao, created_at)
SELECT md5('exec-'||i||'-'||j)::uuid,
       md5('treino-'||i||'-'||(((j-1)%3)+1))::uuid,
       md5('aluno-'||i)::uuid,
       now() - ((j*6)||' days')::interval,
       now() - ((j*6)||' days')::interval
FROM generate_series(1,:n_alunos) i, generate_series(1,:exec_per_aluno) j
ON CONFLICT (id) DO NOTHING;

INSERT INTO execucoes_exercicio (id, execucao_treino_id, treino_exercicio_id, series_executadas, repeticoes_executadas, carga_executada)
SELECT md5('execex-'||i||'-'||j)::uuid,
       md5('exec-'||i||'-'||j)::uuid,
       md5('te-'||i||'-'||(((j-1)%3)+1)||'-'||(((j-1)%6)+1))::uuid,
       3, 10 + (j % 5), (20 + (j % 40))::numeric
FROM generate_series(1,:n_alunos) i, generate_series(1,:exec_per_aluno) j
ON CONFLICT (id) DO NOTHING;

COMMIT;

ANALYZE;

\echo == contagens ==
SELECT 'contas' tabela, count(*) FROM contas
UNION ALL SELECT 'treinadores', count(*) FROM treinadores
UNION ALL SELECT 'alunos', count(*) FROM alunos
UNION ALL SELECT 'vinculos', count(*) FROM vinculos_treinador_aluno
UNION ALL SELECT 'assinaturas', count(*) FROM assinaturas_aluno
UNION ALL SELECT 'pagamentos', count(*) FROM pagamentos
UNION ALL SELECT 'treinos', count(*) FROM treinos
UNION ALL SELECT 'treino_alunos', count(*) FROM treino_alunos
UNION ALL SELECT 'execucoes_treino', count(*) FROM execucoes_treino
UNION ALL SELECT 'execucoes_exercicio', count(*) FROM execucoes_exercicio;
