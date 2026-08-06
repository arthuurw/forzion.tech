#!/usr/bin/env bash
# Repro (DEPLOY-03/04): prova o gate nginx -t (T003) e o edge-probe (T004) adicionados no
# deploy. (a) nginx.conf quebrado -> nginx -t exit != 0 (o gate abortaria antes do restart).
# (b) config válida -> nginx -t exit 0. (c) edge: nginx real na frente de um upstream dummy
# responde 200 com Host correto; proxy_pass pra upstream inexistente -> 502 (o que o edge-probe
# pega e o nginx -t sozinho não pegaria, por ser sintaticamente válido -- spec E5). Requer Docker.
set -euo pipefail

WORK="$(mktemp -d)"
NET="nginx-gate-check-net"
UP="nginx-gate-check-upstream"
EDGE="nginx-gate-check-edge"

cleanup() {
  docker rm -f "$UP" "$EDGE" >/dev/null 2>&1 || true
  docker network rm "$NET" >/dev/null 2>&1 || true
  rm -rf "$WORK"
}
trap cleanup EXIT

echo "=== caso (a): nginx.conf quebrado -> nginx -t deve reprovar ==="
cat > "$WORK/broken.conf" <<'EOF'
events {}
http {
  server {
    listen 80
    server_name _;
  }
}
EOF
if docker run --rm -v "$WORK/broken.conf:/etc/nginx/nginx.conf:ro" nginx:1.27-alpine nginx -t >/dev/null 2>&1; then
  echo "nginx-gate-check: FALHOU -- config quebrada passou no nginx -t." >&2
  exit 1
fi
echo "OK -- nginx -t reprovou a config quebrada (exit != 0)."

echo "=== caso (b): nginx.conf válido -> nginx -t deve passar ==="
cat > "$WORK/valid.conf" <<'EOF'
events {}
http {
  server {
    listen 80;
    server_name _;
    location / { return 200 "ok"; }
  }
}
EOF
docker run --rm -v "$WORK/valid.conf:/etc/nginx/nginx.conf:ro" nginx:1.27-alpine nginx -t >/dev/null 2>&1 \
  || { echo "nginx-gate-check: FALHOU -- config válida reprovou no nginx -t." >&2; exit 1; }
echo "OK -- nginx -t aprovou a config válida (exit 0)."

echo "=== caso (c): edge routing (Host correto) via upstream real e quebrado ==="
docker network rm "$NET" >/dev/null 2>&1 || true
docker network create "$NET" >/dev/null

docker rm -f "$UP" >/dev/null 2>&1 || true
docker run -d --name "$UP" --network "$NET" \
  -v "$WORK/valid.conf:/etc/nginx/nginx.conf:ro" \
  nginx:1.27-alpine >/dev/null
sleep 1

cat > "$WORK/edge-ok.conf" <<EOF
events {}
http {
  server {
    listen 80 default_server;
    server_name _;
    return 444;
  }
  server {
    listen 80;
    server_name edge-check.local;
    location / { proxy_pass http://$UP:80; }
  }
}
EOF

docker rm -f "$EDGE" >/dev/null 2>&1 || true
docker run -d --name "$EDGE" --network "$NET" -p 18080:80 \
  -v "$WORK/edge-ok.conf:/etc/nginx/nginx.conf:ro" \
  nginx:1.27-alpine >/dev/null
sleep 1

code="$(curl -s -o /dev/null -w '%{http_code}' --resolve edge-check.local:18080:127.0.0.1 http://edge-check.local:18080/ || true)"
[ "$code" = "200" ] || { echo "nginx-gate-check: FALHOU -- edge com upstream OK devolveu $code (esperado 200)." >&2; exit 1; }
echo "OK -- edge com upstream saudável responde 200 (Host correto)."

# Host desconhecido cai no default_server (444) -- mesma defesa do nginx.conf real (:23-29).
code_unknown="$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:18080/ 2>/dev/null || true)"
[ "$code_unknown" != "200" ] || { echo "nginx-gate-check: FALHOU -- default_server não deveria responder 200." >&2; exit 1; }
echo "OK -- Host desconhecido não atravessa (default_server)."

cat > "$WORK/edge-broken.conf" <<EOF
events {}
http {
  server {
    listen 80 default_server;
    server_name _;
    return 444;
  }
  server {
    listen 80;
    server_name edge-check.local;
    location / { proxy_pass http://upstream-que-nao-existe:80; }
  }
}
EOF
docker rm -f "$EDGE" >/dev/null 2>&1 || true
docker run -d --name "$EDGE" --network "$NET" -p 18080:80 \
  -v "$WORK/edge-broken.conf:/etc/nginx/nginx.conf:ro" \
  nginx:1.27-alpine >/dev/null
sleep 1

code_broken="$(curl -s -o /dev/null -w '%{http_code}' -m 5 --resolve edge-check.local:18080:127.0.0.1 http://edge-check.local:18080/ 2>/dev/null || true)"
[ "$code_broken" != "200" ] || { echo "nginx-gate-check: FALHOU -- proxy_pass quebrado deveria reprovar (502/000), veio 200." >&2; exit 1; }
echo "OK -- proxy_pass quebrado reprova (HTTP $code_broken) -- edge-probe pegaria isso mesmo com nginx -t passando (E5)."

echo "nginx-gate-check: OK -- gate nginx -t (broken/valid) e edge-probe (routing) comportam-se como esperado."
