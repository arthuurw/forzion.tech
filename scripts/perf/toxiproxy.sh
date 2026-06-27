#!/usr/bin/env bash
# Toxiproxy p/ o load test fase4: injeta latência no Postgres (FR-6) e no provider de
# e-mail (FR-5). Idempotente. Requer o container do seed (forzion-perfbench) no ar.
#
#   scripts/perf/toxiproxy.sh up               # sobe proxy + cria os 2 proxies (sem toxic)
#   scripts/perf/toxiproxy.sh toxic-add db 40      # +40ms/conn no proxy do Postgres
#   scripts/perf/toxiproxy.sh toxic-add mail 2000  # +2000ms/call no proxy de e-mail
#   scripts/perf/toxiproxy.sh toxic-clear db|mail  # remove toxics do proxy
#   scripts/perf/toxiproxy.sh status               # lista proxies + toxics
#   scripts/perf/toxiproxy.sh down                 # remove container + rede
#
# Portas (host): admin 8474 · db-proxy 5434 (→ perfbench:5432) · mail-proxy 9098 (→ host:9099 sink)
set -uo pipefail
TOXI=forzion-toxiproxy
NET=perfnet
PERFBENCH=forzion-perfbench
ADMIN=http://localhost:8474
CMD="${1:-status}"

api() { curl -s "$@"; }

case "$CMD" in
  up)
    docker network create "$NET" >/dev/null 2>&1 || true
    docker network connect "$NET" "$PERFBENCH" >/dev/null 2>&1 || true
    if ! docker ps --format '{{.Names}}' | grep -qx "$TOXI"; then
      docker rm -f "$TOXI" >/dev/null 2>&1 || true
      docker run -d --name "$TOXI" --network "$NET" \
        -p 8474:8474 -p 5434:5434 -p 9098:9098 \
        ghcr.io/shopify/toxiproxy >/dev/null
    fi
    for i in $(seq 1 30); do api "$ADMIN/version" >/dev/null 2>&1 && break; sleep 1; done
    # recria proxies (idempotente: delete+create)
    api -X DELETE "$ADMIN/proxies/db"   >/dev/null 2>&1 || true
    api -X DELETE "$ADMIN/proxies/mail" >/dev/null 2>&1 || true
    api -X POST "$ADMIN/proxies" -d "{\"name\":\"db\",\"listen\":\"0.0.0.0:5434\",\"upstream\":\"$PERFBENCH:5432\",\"enabled\":true}" >/dev/null
    api -X POST "$ADMIN/proxies" -d "{\"name\":\"mail\",\"listen\":\"0.0.0.0:9098\",\"upstream\":\"host.docker.internal:9099\",\"enabled\":true}" >/dev/null
    echo ">> toxiproxy up: db(:5434→$PERFBENCH:5432) mail(:9098→host:9099)"
    api "$ADMIN/proxies" | tr ',' '\n' | grep -E 'name|listen|upstream' | head
    ;;
  toxic-add)
    P="${2:?proxy db|mail}"; MS="${3:?latency ms}"
    api -X POST "$ADMIN/proxies/$P/toxics" \
      -d "{\"name\":\"lat_$P\",\"type\":\"latency\",\"attributes\":{\"latency\":$MS,\"jitter\":0}}" >/dev/null
    echo ">> toxic latency ${MS}ms add em '$P'"
    ;;
  toxic-clear)
    P="${2:?proxy db|mail}"
    api -X DELETE "$ADMIN/proxies/$P/toxics/lat_$P" >/dev/null 2>&1 || true
    echo ">> toxics limpos em '$P'"
    ;;
  status)
    api "$ADMIN/proxies" 2>/dev/null | python -m json.tool 2>/dev/null || echo "toxiproxy não responde em $ADMIN"
    ;;
  down)
    docker rm -f "$TOXI" >/dev/null 2>&1 || true
    docker network disconnect "$NET" "$PERFBENCH" >/dev/null 2>&1 || true
    docker network rm "$NET" >/dev/null 2>&1 || true
    echo ">> toxiproxy down"
    ;;
  *) echo "uso: up | toxic-add <db|mail> <ms> | toxic-clear <db|mail> | status | down"; exit 1;;
esac
