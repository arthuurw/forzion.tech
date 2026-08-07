#!/bin/bash
# Sobe/recarrega o nginx de borda unico (docker-compose.edge.yml). Chamado pelos deploys
# apos os backends subirem. EDGE_DIR = checkout dono da config do edge (default homolog).
set -euo pipefail

EDGE_DIR="${EDGE_DIR:-/opt/forzion/app}"
cd "$EDGE_DIR"

for net in forzion-hmg forzion-prd; do
  docker network create "$net" >/dev/null 2>&1 || true
  docker network inspect "$net" >/dev/null 2>&1 || { echo "::error::network $net indisponivel"; exit 1; }
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

conf_hash_host="$(sha256sum nginx/nginx.conf | cut -d' ' -f1)"

# `exec` falha enquanto o container recem-criado ainda esta em `created`: sem o retry o hash
# viria vazio e o deploy reprovaria por CORRIDA, nao por config divergente. `|| true` porque
# sob `set -e` a atribuicao herdaria o exit do pipeline e abortaria o script.
conf_hash_live() {
  local h
  for _ in 1 2 3 4 5; do
    h="$($EDGE exec -T nginx sha256sum /etc/nginx/nginx.conf 2>/dev/null | cut -d' ' -f1)" || true
    if [ -n "$h" ]; then echo "$h"; return 0; fi
    sleep 1
  done
  echo ""
}

$EDGE up -d --remove-orphans

if [ "$(conf_hash_live)" != "$conf_hash_host" ]; then
  # Bind-mount de arquivo unico prende o inode que existia no start do container. `git pull`
  # troca o arquivo por rename => o container segue lendo o inode antigo e `nginx -s reload`
  # rele essa copia morta; so recriar reancora o mount. O `nginx -t` acima roda em container
  # efemero (mount novo), entao aprova o config novo enquanto o vivo continua servindo o velho.
  $EDGE up -d --force-recreate --no-deps nginx
elif [ "$EDGE_WAS_UP" = 1 ]; then
  $EDGE exec -T nginx nginx -s reload
fi

if [ "$(conf_hash_live)" != "$conf_hash_host" ]; then
  echo "::error::edge servindo nginx.conf diferente do checkout ($EDGE_DIR) apos o reload."
  exit 1
fi
