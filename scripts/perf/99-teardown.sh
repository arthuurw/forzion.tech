#!/usr/bin/env bash
# Remove o container efêmero do bench (e seus dados). Schema isolado some junto.
set -euo pipefail
CONTAINER="${CONTAINER:-forzion-perfbench}"
docker rm -f "$CONTAINER" 2>/dev/null && echo ">> removido: $CONTAINER" || echo ">> nada a remover ($CONTAINER)"
