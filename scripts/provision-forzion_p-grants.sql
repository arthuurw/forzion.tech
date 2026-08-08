-- Provisionamento de GRANTs do schema public no projeto Supabase forzion_p (PRODUÇÃO).
-- Rodar UMA VEZ no forzion_p (SQL editor / psql como postgres), ANTES do `app migrate`.
-- NUNCA rodar contra o projeto homolog (forzion) — forzion_p contém dados reais de prod.
--
-- Postura: LEAST-PRIVILEGE. O app conecta SÓ como forzion_api (frontend -> backend .NET -> DB
-- via Session pooler; auth é JWT+BCrypt próprio, NÃO Supabase Auth). Os roles anon/authenticated
-- (Data API / PostgREST) NÃO recebem grant: a chave anon é pública e, com RLS off, exporia toda
-- tabela de prod. Garanta que o Data API esteja DESABILITADO no forzion_p como defesa adicional.
--
-- Owner das tabelas: `app migrate` roda como forzion_api (DB_CONNECTION), então forzion_api é dono
-- do que cria (incl. ai_token_usage, agora criada pela migration AdicionarAiTokenUsage) e herda todos
-- os privilégios. Os GRANT/ALTER DEFAULT abaixo são defesa se algo for criado por OUTRO role e blindam
-- o pg_dump do backup contra "permission denied for sequence".

-- 1. Role de aplicação (idempotente). A senha é setada FORA do repo (dashboard / secret do pooler).
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'forzion_api') THEN
    CREATE ROLE forzion_api LOGIN;
  END IF;
END
$$;

-- 2. Schema: USAGE + CREATE — forzion_api roda as migrations e vira dono das tabelas.
GRANT USAGE, CREATE ON SCHEMA public TO forzion_api;

-- 3. Objetos JÁ existentes (idempotente; redundante para os que forzion_api já possui).
GRANT ALL ON ALL TABLES IN SCHEMA public TO forzion_api;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO forzion_api;

-- 4. Objetos FUTUROS criados por postgres (defesa se algo for provisionado como admin). ALTER DEFAULT
--    PRIVILEGES é por role criador — nomear postgres garante que grants futuros apliquem a forzion_api.
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public
  GRANT ALL ON TABLES TO forzion_api;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO forzion_api;

-- 5. REVOKE explícito de PUBLIC/anon/authenticated.
REVOKE ALL ON SCHEMA public FROM PUBLIC;
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
    REVOKE ALL ON ALL TABLES IN SCHEMA public FROM anon;
    REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM anon;
  END IF;
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
    REVOKE ALL ON ALL TABLES IN SCHEMA public FROM authenticated;
    REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM authenticated;
  END IF;
END
$$;

-- 6. Default privileges de anon/authenticated: REVOKE. O passo 5 cobre só objeto EXISTENTE; sem este
--    passo, objeto criado no futuro COMO postgres (migration pelo dashboard, provisionamento manual)
--    nasce com o grant default do Supabase e fica legível pela chave publicável. Foi essa a lacuna
--    que a auditoria de 2026-08-08 encontrou: o passo 4 ADICIONA forzion_api às default privileges,
--    mas não REMOVE anon/authenticated delas.
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon')
     AND EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
    EXECUTE 'ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public REVOKE ALL ON TABLES FROM anon, authenticated';
    EXECUTE 'ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public REVOKE ALL ON SEQUENCES FROM anon, authenticated';
    EXECUTE 'ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public REVOKE ALL ON FUNCTIONS FROM anon, authenticated';
  END IF;
END
$$;

-- LIMITE CONHECIDO — o criador `supabase_admin` NÃO é alcançável por este script:
--   ALTER DEFAULT PRIVILEGES FOR ROLE supabase_admin ... => ERROR 42501 permission denied
-- (exige membership em supabase_admin; o `postgres` do Supabase gerenciado não tem). O default de
-- TABLES desse criador concede anon=arwdDxtm, então objeto criado em public POR supabase_admin nasce
-- exposto e não há como impedir pelo plano de dados. Por isso DESABILITAR A DATA API é o controle
-- PRIMÁRIO, não o secundário: anon/authenticated só são alcançáveis externamente via PostgREST.
-- Este script é a segunda camada, para o caso de a Data API ser religada.
--
-- VERIFICAÇÃO (não confiar em information_schema.role_table_grants — a view filtra por membership do
-- usuário corrente e devolve falso-zero):
--   SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
--    WHERE n.nspname='public' AND c.relkind='r' AND has_table_privilege('anon', c.oid, 'SELECT');
-- Esperado: 0.
