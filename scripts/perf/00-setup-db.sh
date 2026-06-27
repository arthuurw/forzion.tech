#!/usr/bin/env bash
# Sobe Postgres efêmero (Docker), aplica as migrations EF no schema isolado
# perf_bench e roda o seed sintético. Idempotente: reexecutar reusa o container
# e o seed (ON CONFLICT DO NOTHING). Para zerar: scripts/perf/99-teardown.sh
#
# Pré-req: Docker Desktop no ar; dotnet SDK 10; rodar da RAIZ do repo.
# Env (override opcional): CONTAINER, PORT, DB, SCHEMA, N_TREINADORES, N_ALUNOS, EXEC_PER_ALUNO
set -euo pipefail

CONTAINER="${CONTAINER:-forzion-perfbench}"
PORT="${PORT:-55432}"
DB="${DB:-forzion_bench}"
SCHEMA="${SCHEMA:-perf_bench}"
PGPASS="${PGPASS:-bench}"
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"

echo ">> [1/4] container $CONTAINER"
if ! docker ps -a --format '{{.Names}}' | grep -qx "$CONTAINER"; then
  docker run -d --name "$CONTAINER" -e POSTGRES_PASSWORD="$PGPASS" -e POSTGRES_DB="$DB" \
    -p "$PORT:5432" postgres:17-alpine
fi
docker start "$CONTAINER" >/dev/null 2>&1 || true
for i in $(seq 1 60); do
  docker exec "$CONTAINER" pg_isready -U postgres >/dev/null 2>&1 && break
  sleep 1
done

echo ">> [2/4] schema isolado $SCHEMA"
docker exec "$CONTAINER" psql -U postgres -d "$DB" -c "CREATE SCHEMA IF NOT EXISTS $SCHEMA;"

echo ">> [3/4] migrations EF → $SCHEMA (search_path; AppDbContextFactory lê ConnectionStrings__AppConnection)"
ConnectionStrings__AppConnection="Host=localhost;Port=$PORT;Database=$DB;Username=postgres;Password=$PGPASS;Search Path=$SCHEMA" \
ASPNETCORE_ENVIRONMENT=Development \
dotnet ef database update \
  --project "$ROOT/forzion.tech.Infrastructure" \
  --startup-project "$ROOT/forzion.tech.Api"

echo ">> [4/4] seed sintético"
docker exec -i -e PGOPTIONS="--search_path=$SCHEMA" \
  "$CONTAINER" psql -U postgres -d "$DB" -q \
  -v n_treinadores="${N_TREINADORES:-200}" \
  -v n_alunos="${N_ALUNOS:-5000}" \
  -v exec_per_aluno="${EXEC_PER_ALUNO:-60}" \
  -f - < "$HERE/seed-bench.sql"

echo ">> pronto. EXPLAIN: scripts/perf/run-explain.sh | teardown: scripts/perf/99-teardown.sh"
