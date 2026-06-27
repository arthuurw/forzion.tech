#!/usr/bin/env bash
# Curva de contenção do request-path: roda as queries quentes do aluno em
# concorrência crescente (-c) e captura tps + latência média. Mostra como o
# PG-alvo degrada sob carga (proxy do cliff quando a concorrência excede CPU/conns).
# Roda DENTRO do container (localhost, sem RTT de rede). search_path=perf_bench.
#
# Uso: scripts/perf/load/run-pgbench.sh
# Env: CONTAINER, DB, PGUSER, SCHEMA, DURATION (s/passo), CLIENTS (lista)
set -euo pipefail
CONTAINER="${CONTAINER:-forzion-perfbench}"
DB="${DB:-forzion_bench}"
PGUSER="${PGUSER:-postgres}"
SCHEMA="${SCHEMA:-perf_bench}"
DURATION="${DURATION:-8}"
CLIENTS="${CLIENTS:-1 4 8 16 32 64}"
HERE="$(cd "$(dirname "$0")" && pwd)"

# stdin → arquivo no container (evita mangling de path do git-bash em docker cp)
docker exec -i "$CONTAINER" sh -c 'cat > /tmp/rp.sql' < "$HERE/pgbench-request-path.sql"

printf '%-9s %-12s %-16s %-14s\n' clients tps lat_avg_ms stddev_ms
for c in $CLIENTS; do
  j=$(( c < 8 ? c : 8 ))
  out=$(MSYS_NO_PATHCONV=1 docker exec -e PGOPTIONS="--search_path=$SCHEMA" "$CONTAINER" \
        pgbench -U "$PGUSER" -d "$DB" -n -c "$c" -j "$j" -T "$DURATION" -f /tmp/rp.sql 2>&1)
  tps=$(echo "$out" | grep -oE 'tps = [0-9.]+' | head -1 | grep -oE '[0-9.]+')
  lat=$(echo "$out" | grep -oE 'latency average = [0-9.]+ ms' | grep -oE '[0-9.]+')
  std=$(echo "$out" | grep -oE 'latency stddev = [0-9.]+ ms' | grep -oE '[0-9.]+' || echo '-')
  printf '%-9s %-12s %-16s %-14s\n' "$c" "${tps:-?}" "${lat:-?}" "${std:-?}"
done
