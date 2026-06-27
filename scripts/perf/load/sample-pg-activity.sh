#!/usr/bin/env bash
# Amostra pg_stat_activity durante um teste de carga: conexões active /
# idle-in-transaction / waiting, a cada INTERVAL s por DURATION s. Saída CSV.
# Roda em paralelo ao k6 p/ flagrar starvation de pool (idle-in-tx alto +
# waiting > 0 = backlog atrás do pool).
#
# Uso: scripts/perf/load/sample-pg-activity.sh [out.csv]
# Env: CONTAINER, DB, PGUSER, INTERVAL (s), DURATION (s)
set -euo pipefail
CONTAINER="${CONTAINER:-forzion-perfbench}"
DB="${DB:-forzion_bench}"
PGUSER="${PGUSER:-postgres}"
INTERVAL="${INTERVAL:-1}"
DURATION="${DURATION:-120}"
OUT="${1:-pg-activity.csv}"

echo "ts,total,active,idle_in_tx,waiting" > "$OUT"
end=$(( $(date +%s) + DURATION ))
while [ "$(date +%s)" -lt "$end" ]; do
  row=$(docker exec "$CONTAINER" psql -U "$PGUSER" -d "$DB" -At -F',' -c "
    SELECT
      count(*),
      count(*) FILTER (WHERE state='active'),
      count(*) FILTER (WHERE state='idle in transaction'),
      count(*) FILTER (WHERE wait_event_type IS NOT NULL AND state='active')
    FROM pg_stat_activity WHERE datname='$DB' AND pid<>pg_backend_pid();")
  echo "$(date +%s),$row" >> "$OUT"
  sleep "$INTERVAL"
done
echo ">> $OUT"
