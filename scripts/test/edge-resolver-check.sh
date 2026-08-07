#!/usr/bin/env bash
# Prova a mitigacao do restart-coupling da borda unica: upstream por VARIAVEL + `resolver` resolve
# em runtime, entao um upstream inexistente deixa de impedir o nginx de SUBIR (com hostname literal
# ele resolve no parse e recusa -- reboot com homolog fora derrubaria producao junto).
# (a) discrimina literal x variavel no `nginx -t`; (b) prova que $request_uri preserva path+query;
# (c) prova que upstream ausente vira 502 em vez de derrubar a borda. Requer Docker.
set -euo pipefail

[ -d /opt/forzion ] && { echo "recusando rodar em host de deploy." >&2; exit 1; }

# docker.exe nao entende path MSYS (/tmp/...): converte o lado HOST dos binds quando houver cygpath.
WORK="$(mktemp -d)"
WORK_BIND="$(command -v cygpath >/dev/null 2>&1 && cygpath -m "$WORK" || echo "$WORK")"
NET="edge-resolver-check-net"
UP="edge-resolver-check-upstream"
EDGE="edge-resolver-check-edge"
PORT=18083

cleanup() {
  docker rm -f "$UP" "$EDGE" >/dev/null 2>&1 || true
  docker network rm "$NET" >/dev/null 2>&1 || true
  rm -rf "$WORK"
}
trap cleanup EXIT

conf_edge() { # $1 = diretiva de upstream
  cat > "$WORK/$2" <<EOF
events {}
http {
  resolver 127.0.0.11 valid=10s ipv6=off;
  resolver_timeout 5s;
  server {
    listen 80;
    server_name _;
    location / {
      $1
      proxy_set_header Host \$host;
      proxy_connect_timeout 5s;
    }
  }
}
EOF
}

echo "=== caso (a): upstream inexistente -- literal reprova, variavel passa ==="
conf_edge "proxy_pass http://upstream-que-nao-existe:80;" literal.conf
if docker run --rm -v "$WORK_BIND/literal.conf:/etc/nginx/nginx.conf:ro" nginx:1.27-alpine nginx -t >/dev/null 2>&1; then
  echo "FALHOU -- literal com upstream ausente deveria reprovar no nginx -t." >&2
  exit 1
fi
echo "OK -- literal reprova (host not found in upstream), como hoje."

conf_edge "set \$up upstream-que-nao-existe;
      proxy_pass http://\$up:80\$request_uri;" variavel.conf
docker run --rm -v "$WORK_BIND/variavel.conf:/etc/nginx/nginx.conf:ro" nginx:1.27-alpine nginx -t >/dev/null 2>&1 \
  || { echo "FALHOU -- variavel com upstream ausente deveria PASSAR no nginx -t." >&2; exit 1; }
echo "OK -- variavel passa: a borda sobe mesmo sem o upstream existir."

echo "=== caso (b): com upstream real, \$request_uri preserva path e query ==="
docker network rm "$NET" >/dev/null 2>&1 || true
docker network create "$NET" >/dev/null

cat > "$WORK/upstream.conf" <<'EOF'
events {}
http {
  server {
    listen 80;
    server_name _;
    location / { default_type text/plain; return 200 "$request_uri"; }
  }
}
EOF
docker run -d --name "$UP" --network "$NET" \
  -v "$WORK_BIND/upstream.conf:/etc/nginx/nginx.conf:ro" nginx:1.27-alpine >/dev/null

conf_edge "set \$up $UP;
      proxy_pass http://\$up:80\$request_uri;" real.conf
docker run -d --name "$EDGE" --network "$NET" -p "$PORT:80" \
  -v "$WORK_BIND/real.conf:/etc/nginx/nginx.conf:ro" nginx:1.27-alpine >/dev/null
sleep 2

alvo="/algum/caminho?a=1&b=dois"
corpo="$(curl -s -m 5 "http://127.0.0.1:$PORT$alvo" || true)"
[ "$corpo" = "$alvo" ] || { echo "FALHOU -- upstream recebeu '$corpo', esperado '$alvo'." >&2; exit 1; }
echo "OK -- upstream recebeu a URI intacta ($corpo)."

echo "=== caso (c): upstream some em runtime -> 502, borda continua de pe ==="
docker rm -f "$UP" >/dev/null
code="$(curl -s -o /dev/null -w '%{http_code}' -m 8 "http://127.0.0.1:$PORT/" || true)"
[ "$code" = "502" ] || [ "$code" = "504" ] \
  || { echo "FALHOU -- esperado 502/504 com upstream ausente, veio '$code'." >&2; exit 1; }
docker ps --format '{{.Names}}' | grep -qx "$EDGE" \
  || { echo "FALHOU -- a borda caiu junto com o upstream." >&2; exit 1; }
echo "OK -- upstream ausente devolve $code e a borda segue no ar."

echo "edge-resolver-check: OK -- resolucao em runtime desacopla a borda do ciclo de vida dos upstreams."
