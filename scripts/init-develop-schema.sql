-- Stack local (docker-compose.yml): cria o schema `develop` (sandbox local).
-- As migrations são schema-agnostic e aplicam no Search Path da connection (=develop).
-- `homolog` é o schema do ambiente hospedado (Hostinger), não do local. Ver specs/specification-db.md.
CREATE SCHEMA IF NOT EXISTS develop;
