-- Patches p/ o full-app load test (fase4). Roda DEPOIS do seed, no schema perf_bench.
-- 1) torna contas bench logáveis: o seed grava password_hash placeholder (NÃO é bcrypt válido).
-- 2) move assinaturas Ativa p/ a janela de pré-aviso [hoje+3d, hoje+4d) → o lote tem fan-out.
-- Senha de todas as contas patchadas: Bench@123456
--   docker exec -i -e PGOPTIONS=--search_path=perf_bench forzion-perfbench \
--     psql -U postgres -d forzion_bench -f - < scripts/perf/patch-loadtest.sql
CREATE EXTENSION IF NOT EXISTS pgcrypto;

UPDATE contas
SET password_hash = crypt('Bench@123456', gen_salt('bf', 11))
WHERE email IN (
  'aluno1@bench.local','aluno2@bench.local','aluno3@bench.local','aluno4@bench.local',
  'aluno5@bench.local','aluno6@bench.local','aluno7@bench.local','aluno8@bench.local',
  'treinador1@bench.local','treinador2@bench.local');

-- 3) conta SystemAdmin bench (não vem do seed) p/ o Lighthouse autenticado do /admin (FR-4).
--    Exige a linha em system_users senão o LoginPerfilResolver lança 500.
INSERT INTO contas (id, email, password_hash, tipo_conta, created_at, email_verificado, verificado_em)
VALUES (md5('conta-admin-bench')::uuid, 'admin1@bench.local',
        crypt('Bench@123456', gen_salt('bf',11)), 'SystemAdmin', now(), true, now())
ON CONFLICT (email) DO UPDATE SET password_hash=EXCLUDED.password_hash, tipo_conta='SystemAdmin', email_verificado=true;
INSERT INTO system_users (id, conta_id, nome, role, status, created_at)
VALUES (md5('sysuser-admin-bench')::uuid, md5('conta-admin-bench')::uuid, 'Admin Bench', 'SuperAdmin', 'Ativo', now())
ON CONFLICT (id) DO NOTHING;

UPDATE assinaturas_aluno
SET data_proxima_cobranca = (current_date + 3) + interval '12 hours'
WHERE status = 'Ativa';

ANALYZE assinaturas_aluno;

\echo == due no window (status Ativa, [+3d,+4d)) ==
SELECT count(*) AS due_window FROM assinaturas_aluno
WHERE status='Ativa' AND data_proxima_cobranca >= (current_date+3) AND data_proxima_cobranca < (current_date+4);
\echo == contas logaveis ==
SELECT count(*) AS logaveis FROM contas WHERE length(password_hash)=60 AND email LIKE '%@bench.local';
