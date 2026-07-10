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
-- o pg_dump do backup contra "permission denied for sequence" (incidentes #260/#271).

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
