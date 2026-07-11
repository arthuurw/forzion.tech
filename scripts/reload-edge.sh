#!/bin/bash
# Sobe/recarrega o nginx de borda unico (docker-compose.edge.yml). Chamado pelos deploys
# apos os backends subirem. EDGE_DIR = checkout dono da config do edge (default homolog).
set -euo pipefail

EDGE_DIR="${EDGE_DIR:-/opt/forzion/app}"
cd "$EDGE_DIR"

for net in forzion-hmg forzion-prd; do
  docker network inspect "$net" >/dev/null 2>&1 || docker network create "$net"
done

EDGE="docker compose -p edge -f docker-compose.edge.yml"

if $EDGE ps --services --filter status=running 2>/dev/null | grep -qx nginx; then
  EDGE_WAS_UP=1
else
  EDGE_WAS_UP=0
fi

if ! $EDGE run --rm --no-deps nginx nginx -t; then
  echo "::error::nginx.conf (edge) invalido — abortando ANTES de tocar no edge."
  exit 1
fi

$EDGE up -d --remove-orphans

if [ "$EDGE_WAS_UP" = 1 ]; then
  $EDGE exec -T nginx nginx -s reload
fi
