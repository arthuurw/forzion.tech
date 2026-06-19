#!/usr/bin/env bash
set -euo pipefail

# --volumes OMITIDO: apagaria pact_pgdata e dados persistentes.
docker builder prune -f --keep-storage 5g
docker image prune -f
journalctl --vacuum-time=14d >/dev/null 2>&1 || true
