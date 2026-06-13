#!/usr/bin/env bash
# R3 (deploy-safety): dry-run do migrate contra uma CÓPIA do schema homolog real, na VM.
# Fluxo: sobe um Postgres efêmero (docker-compose.dryrun.yml) → restaura nele o schema homolog do
# DB real (pg_dump estrutura+dados, incluindo __EFMigrationsHistory → só migrations PENDENTES
# rodam) → roda `app migrate` contra a cópia. Falha aqui aborta o deploy (set -e) sem tocar o real,
# pegando falha data-dependente (ex.: índice UNIQUE sobre linha duplicada) invisível ao CI em DB
# vazio. Respeita C1 (pg_dump direto, sem chave de backup) e C5 (a VM/containers alcançam o pooler).
#
# Requer no ambiente: DB_CONNECTION (string Npgsql do DB homolog real). ENV_FILE default /opt/forzion/.env.
set -euo pipefail

DRYRUN_FILE="${DRYRUN_FILE:-docker-compose.dryrun.yml}"
ENV_FILE="${ENV_FILE:-/opt/forzion/.env}"
DB_CONNECTION="${DB_CONNECTION:?DB_CONNECTION (Npgsql do homolog real) é obrigatório}"

# Projeto DEDICADO: docker-compose.homolog.yml e este vivem no MESMO dir, logo herdariam o mesmo
# project name default ("app"). Sem `-p` próprio, `down --remove-orphans` aqui apagaria os
# containers LIVE do homolog (tratados como órfãos). Isolar é obrigatório.
DC=(docker compose -p forzion-dryrun -f "$DRYRUN_FILE" --env-file "$ENV_FILE")

# Extrai um par chave=valor da string Npgsql (case-insensitive; tolera espaço na chave, ex. "User Id").
get_kv() {
  printf '%s' "$DB_CONNECTION" | tr ';' '\n' \
    | sed -E 's/^[[:space:]]+//' \
    | grep -iE "^($1)[[:space:]]*=" | head -1 \
    | sed -E 's/^[^=]+=[[:space:]]*//'
}

SRC_HOST="$(get_kv 'host|server')"
SRC_PORT="$(get_kv 'port')"; SRC_PORT="${SRC_PORT:-5432}"
SRC_DB="$(get_kv 'database')"
SRC_USER="$(get_kv 'username|user id|user')"
SRC_PASS="$(get_kv 'password|pwd')"
SCHEMA="$(get_kv 'search path|searchpath')"; SCHEMA="${SCHEMA:-${DRYRUN_SCHEMA:-homolog}}"

[ -n "$SRC_HOST" ] && [ -n "$SRC_DB" ] && [ -n "$SRC_USER" ] || {
  echo "migrate-dryrun: não consegui parsear host/database/user de DB_CONNECTION." >&2; exit 1; }

cleanup() { "${DC[@]}" down -v --remove-orphans >/dev/null 2>&1 || true; }
trap cleanup EXIT

echo "migrate-dryrun: subindo Postgres efêmero..."
"${DC[@]}" up -d dryrun-db

# Espera o healthcheck do dryrun-db (pg_isready).
state=""
for _ in $(seq 1 40); do
  cid="$("${DC[@]}" ps -q dryrun-db)"
  state="$(docker inspect -f '{{.State.Health.Status}}' "$cid" 2>/dev/null || true)"
  [ "$state" = healthy ] && break
  sleep 2
done
[ "$state" = healthy ] || { echo "migrate-dryrun: dryrun-db não ficou healthy." >&2; exit 1; }

echo "migrate-dryrun: clonando schema '$SCHEMA' do DB real para a cópia..."
# pg_dump cria o schema e popula; pipe interno ao container (alcança o pooler via NAT da VM).
"${DC[@]}" exec -T -e PGPASSWORD="$SRC_PASS" dryrun-db sh -c \
  "pg_dump 'host=$SRC_HOST port=$SRC_PORT user=$SRC_USER dbname=$SRC_DB sslmode=require' \
     --schema='$SCHEMA' --no-owner --no-privileges \
   | psql -U dryrun -d dryrun -v ON_ERROR_STOP=1 -q"

echo "migrate-dryrun: aplicando migrations na cópia (app migrate)..."
"${DC[@]}" build migrate
"${DC[@]}" run --rm migrate

echo "migrate-dryrun: OK — migrate aplicou limpo na cópia do schema real."
