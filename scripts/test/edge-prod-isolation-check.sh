#!/usr/bin/env bash
# Roda o nginx.conf REAL na borda co-locada com a stack de prd AUSENTE e prova que homolog
# nao e afetado -- o risco que manteve o `:443` de prod comentado ate a mitigacao do resolver
# (run 29203875745: `host not found in upstream "backend-prd"` derrubava o deploy de homolog).
# (a) a borda SOBE sem prd; (b) homolog responde 200; (c) prod :80 redireciona; (d) prod :443
# devolve 502/504 sem contaminar homolog; (e) mutacao p/ upstream literal volta a reprovar.
# Requer Docker.
set -euo pipefail

[ -d /opt/forzion ] && { echo "recusando rodar em host de deploy." >&2; exit 1; }

CONF="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/nginx/nginx.conf"
[ -f "$CONF" ] || { echo "nginx.conf nao encontrado em $CONF" >&2; exit 1; }

# docker.exe nao entende path MSYS (/tmp/...): converte o lado HOST dos binds quando houver cygpath.
tomount() { command -v cygpath >/dev/null 2>&1 && cygpath -m "$1" || echo "$1"; }

WORK="$(mktemp -d)"
WORK_BIND="$(tomount "$WORK")"
CONF_BIND="$(tomount "$CONF")"
NET="edge-prod-isolation-net"
HMG="edge-prod-isolation-hmg"
EDGE="edge-prod-isolation-edge"
PORT_HTTP=18084
PORT_HTTPS=18445

cleanup() {
  docker rm -f "$HMG" "$EDGE" >/dev/null 2>&1 || true
  docker network rm "$NET" >/dev/null 2>&1 || true
  rm -rf "$WORK"
}
trap cleanup EXIT
cleanup

# A imagem do nginx nao traz openssl: gera os certs dummy num alpine.
docker run --rm -v "$WORK_BIND:/le" alpine:3 sh -c '
  apk add --no-cache openssl >/dev/null 2>&1
  for d in homologacao.forzion.tech forzion.tech; do
    mkdir -p /le/le/live/$d
    openssl req -x509 -newkey rsa:2048 -nodes -days 1 -subj "/CN=$d" \
      -keyout /le/le/live/$d/privkey.pem -out /le/le/live/$d/fullchain.pem 2>/dev/null
    [ -s /le/le/live/$d/fullchain.pem ] || exit 1
  done' || { echo "falha gerando certs dummy." >&2; exit 1; }

docker network create "$NET" >/dev/null

cat > "$WORK/upstream.conf" <<'EOF'
events {}
http {
  server {
    listen 80;   server_name _;
    location / { default_type text/plain; return 200 "hmg-ok"; }
  }
  server {
    listen 3000; server_name _;
    location / { default_type text/plain; return 200 "hmg-ok"; }
  }
  server {
    listen 8080; server_name _;
    location / { default_type text/plain; return 200 "hmg-ok"; }
  }
  server {
    listen 9292; server_name _;
    location / { default_type text/plain; return 200 "hmg-ok"; }
  }
}
EOF

# So os upstreams de HOMOLOG existem. backend-prd/frontend-prd ficam de fora de proposito.
docker run -d --name "$HMG" --network "$NET" \
  --network-alias backend-hmg --network-alias frontend-hmg --network-alias pact-broker \
  -v "$WORK_BIND/upstream.conf:/etc/nginx/nginx.conf:ro" nginx:1.27-alpine >/dev/null

echo "=== caso (a): a borda SOBE com a stack de prd ausente ==="
docker run -d --name "$EDGE" --network "$NET" \
  -p "$PORT_HTTP:80" -p "$PORT_HTTPS:443" \
  -v "$CONF_BIND:/etc/nginx/nginx.conf:ro" \
  -v "$WORK_BIND/le:/etc/letsencrypt:ro" nginx:1.27-alpine >/dev/null
sleep 3
docker ps --format '{{.Names}}' | grep -qx "$EDGE" \
  || { echo "FALHOU -- a borda nao subiu sem a stack de prd:" >&2; docker logs "$EDGE" 2>&1 | tail -5 >&2; exit 1; }
echo "OK -- borda no ar mesmo sem backend-prd/frontend-prd."

echo "=== caso (b): homolog responde normalmente ==="
corpo="$(curl -sk -m 10 --resolve "homologacao.forzion.tech:$PORT_HTTPS:127.0.0.1" \
  "https://homologacao.forzion.tech:$PORT_HTTPS/" || true)"
[ "$corpo" = "hmg-ok" ] || { echo "FALHOU -- homolog devolveu '$corpo', esperado 'hmg-ok'." >&2; exit 1; }
echo "OK -- homolog serve pelo upstream correto."

echo "=== caso (c): prod :80 redireciona pra https (nao mais 503) ==="
loc="$(curl -s -o /dev/null -m 10 -w '%{http_code} %{redirect_url}' \
  --resolve "forzion.tech:$PORT_HTTP:127.0.0.1" "http://forzion.tech:$PORT_HTTP/painel?x=1" || true)"
case "$loc" in
  "301 https://forzion.tech/painel?x=1") echo "OK -- 301 preservando path e query." ;;
  *) echo "FALHOU -- esperado 301 p/ https com path+query, veio '$loc'." >&2; exit 1 ;;
esac

echo "=== caso (d): prod :443 sem upstream -> 502/504, homolog INTACTO ==="
code="$(curl -sk -o /dev/null -w '%{http_code}' -m 12 \
  --resolve "forzion.tech:$PORT_HTTPS:127.0.0.1" "https://forzion.tech:$PORT_HTTPS/" || true)"
[ "$code" = "502" ] || [ "$code" = "504" ] \
  || { echo "FALHOU -- esperado 502/504 em prod sem stack, veio '$code'." >&2; exit 1; }
corpo2="$(curl -sk -m 10 --resolve "homologacao.forzion.tech:$PORT_HTTPS:127.0.0.1" \
  "https://homologacao.forzion.tech:$PORT_HTTPS/" || true)"
[ "$corpo2" = "hmg-ok" ] || { echo "FALHOU -- homolog quebrou depois do erro de prod: '$corpo2'." >&2; exit 1; }
echo "OK -- prod devolve $code e homolog segue servindo."

echo "=== caso (e): mutacao -- upstream de prd LITERAL volta a reprovar ==="
sed -E 's#set \$up_frontend_prd frontend-prd;##; s#http://\$up_frontend_prd:3000\$request_uri#http://frontend-prd:3000#' \
  "$CONF" > "$WORK/literal.conf"
grep -q 'http://frontend-prd:3000' "$WORK/literal.conf" \
  || { echo "FALHOU -- a mutacao nao foi aplicada (o conf mudou de forma?)." >&2; exit 1; }
if docker run --rm -v "$WORK_BIND/literal.conf:/etc/nginx/nginx.conf:ro" \
     -v "$WORK_BIND/le:/etc/letsencrypt:ro" nginx:1.27-alpine nginx -t >/dev/null 2>&1; then
  echo "FALHOU -- upstream literal ausente deveria reprovar no nginx -t." >&2
  exit 1
fi
echo "OK -- a mutacao reprova: o teste discrimina de verdade."

echo "edge-prod-isolation-check: OK -- o :443 de prod nao acopla o ciclo de vida de homolog."
