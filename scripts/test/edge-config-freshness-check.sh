#!/usr/bin/env bash
# Repro do edge servindo config velha: (a) prova o mecanismo -- mount de arquivo unico fica
# preso ao inode do start, e `git pull` (rename) nao chega no container; (b) roda o
# reload-edge.sh real contra um EDGE_DIR temporario e exige que a config VIVA passe a ser a
# nova depois de um rename (antes do fix o `nginx -s reload` deixava a velha no ar). Os dois
# casos montam o arquivo a partir de um volume: o bind do Docker Desktop/Windows propaga o
# rename e mascararia a semantica de inode do Linux, que e a do edge real. Requer Docker.
set -euo pipefail

[ -d /opt/forzion ] && { echo "recusando rodar em host de deploy (-p edge derrubaria o edge real)." >&2; exit 1; }

# Todo path deste script e do LADO CONTAINER (`dst=/etc/test.conf`, `exec ... /etc/test.conf`):
# o git-bash os reescreveria pra `C:/Program Files/Git/etc/...` e o docker recusaria o mount.
# Seguro globalmente aqui porque os mounts saem de volumes, nao de bind do host.
export MSYS_NO_PATHCONV=1

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WORK="$(mktemp -d)"
VOL="edge-freshness-check-vol"
VOL_MECH="edge-freshness-check-mech"
CTR="edge-freshness-check-ctr"
PORT=18082

# `-f` com path absoluto quebra no git-bash (docker.exe nao entende /tmp/...): sempre relativo.
edge_down() { (cd "$WORK" && docker compose -p edge -f docker-compose.edge.yml down) >/dev/null 2>&1 || true; }

cleanup() {
  docker rm -f "$CTR" >/dev/null 2>&1 || true
  edge_down
  docker volume rm -f "$VOL" "$VOL_MECH" >/dev/null 2>&1 || true
  rm -rf "$WORK"
}
trap cleanup EXIT

echo "=== caso (a): mount de arquivo unico + rename -> container fica no inode velho ==="
docker volume create "$VOL_MECH" >/dev/null
docker run --rm -v "$VOL_MECH:/data" alpine:3 sh -c 'echo VERSAO_1 > /data/conf'
docker run -d --name "$CTR" \
  --mount "type=volume,src=$VOL_MECH,dst=/etc/test.conf,volume-subpath=conf" \
  alpine:3 sleep 120 >/dev/null

inicial="$(docker exec "$CTR" cat /etc/test.conf)"
[ "$inicial" = "VERSAO_1" ] || { echo "FALHOU -- leitura inicial veio '$inicial'." >&2; exit 1; }

docker run --rm -v "$VOL_MECH:/data" alpine:3 sh -c 'echo VERSAO_2 > /data/conf.new; mv -f /data/conf.new /data/conf'
fonte="$(docker run --rm -v "$VOL_MECH:/data" alpine:3 cat /data/conf)"
[ "$fonte" = "VERSAO_2" ] || { echo "FALHOU -- fonte devia estar em VERSAO_2, veio '$fonte'." >&2; exit 1; }

visto="$(docker exec "$CTR" cat /etc/test.conf)"
[ "$visto" = "VERSAO_1" ] || { echo "FALHOU -- container devia estar preso em VERSAO_1, veio '$visto'." >&2; exit 1; }
docker rm -f "$CTR" >/dev/null
docker volume rm -f "$VOL_MECH" >/dev/null
echo "OK -- mecanismo exercitado (container leu '$visto' apos o rename para VERSAO_2)."

echo "=== caso (b): reload-edge.sh deve deixar a config VIVA igual a do checkout ==="
mkdir -p "$WORK/nginx"
# O mount do nginx.conf vem de um volume (arquivo unico dentro do FS Linux) em vez de bind do
# host: e o MESMO mecanismo do edge real, mas com a semantica de inode do Linux garantida --
# no bind do Docker Desktop/Windows o rename propaga e o caso deixaria de discriminar.
cat > "$WORK/docker-compose.edge.yml" <<EOF
services:
  nginx:
    image: nginx:1.27-alpine
    ports:
      - "$PORT:80"
    volumes:
      - type: volume
        source: $VOL
        target: /etc/nginx/nginx.conf
        read_only: true
        volume:
          subpath: conf
    networks:
      - forzion-hmg
      - forzion-prd

networks:
  forzion-hmg:
    external: true
  forzion-prd:
    external: true

volumes:
  $VOL:
    external: true
EOF

edge_down
docker volume rm -f "$VOL" >/dev/null 2>&1 || true
docker volume create "$VOL" >/dev/null

escreve_conf() {
  cat > "$WORK/nginx/nginx.conf" <<EOF
events {}
http {
  server {
    listen 80;
    server_name _;
    location / { default_type text/plain; return 200 "$1"; }
  }
}
EOF
  docker run --rm -i -v "$VOL:/data" alpine:3 \
    sh -c 'cat > /data/conf.new && mv -f /data/conf.new /data/conf' < "$WORK/nginx/nginx.conf"
}

servido() {
  curl -s -m 5 "http://127.0.0.1:$PORT/" || true
}

escreve_conf V1
EDGE_DIR="$WORK" bash "$REPO_ROOT/scripts/reload-edge.sh" >/dev/null
sleep 1
corpo="$(servido)"
[ "$corpo" = "V1" ] || { echo "FALHOU -- edge devia servir V1, veio '$corpo'." >&2; exit 1; }
echo "OK -- primeiro reload serve V1."

escreve_conf V2
EDGE_DIR="$WORK" bash "$REPO_ROOT/scripts/reload-edge.sh" >/dev/null
sleep 1
corpo="$(servido)"
[ "$corpo" = "V2" ] || {
  echo "FALHOU -- checkout esta em V2 mas o edge continua servindo '$corpo' (config viva stale)." >&2
  exit 1
}
echo "OK -- reload apos rename passa a servir V2 (config viva == checkout)."

echo "edge-config-freshness-check: OK -- reload-edge.sh reancora o mount e verifica a config viva."
