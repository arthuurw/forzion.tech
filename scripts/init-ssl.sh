#!/bin/bash
# Emite o 1o certificado Let's Encrypt (webroot ACME) para um conjunto de dominios.
# Rodar UMA VEZ por ambiente apos DNS + setup-vm. Renew recorrente = servico certbot do edge.
# Uso:
#   bash scripts/init-ssl.sh seu@email.com homologacao.forzion.tech pact.homologacao.forzion.tech
#   bash scripts/init-ssl.sh seu@email.com forzion.tech www.forzion.tech app.forzion.tech
set -euo pipefail

EMAIL="${1:?Uso: $0 <email> <dominio> [dominio...]}"
shift
[ "$#" -ge 1 ] || { echo "Informe ao menos 1 dominio."; exit 1; }

EDGE_DIR="${EDGE_DIR:-/opt/forzion/app}"
cd "$EDGE_DIR"

CERT_ARGS=()
for d in "$@"; do CERT_ARGS+=( -d "$d" ); done

EDGE="docker compose -p edge -f docker-compose.edge.yml"

issue() {
  docker run --rm \
    -v "$EDGE_DIR/certbot/conf:/etc/letsencrypt" \
    -v "$EDGE_DIR/certbot/www:/var/www/certbot" \
    certbot/certbot certonly --webroot --webroot-path=/var/www/certbot \
      --email "$EMAIL" --agree-tos --no-eff-email "${CERT_ARGS[@]}"
}

# Edge no ar (ex.: cert de PROD com homolog LIVE) → emite via webroot do edge, sem downtime.
if $EDGE ps --services --filter status=running 2>/dev/null | grep -qx nginx; then
  issue
  $EDGE exec -T nginx nginx -s reload
else
  docker rm -f nginx-acme 2>/dev/null || true
  docker run --rm -d --name nginx-acme -p 80:80 \
    -v "$EDGE_DIR/certbot/www:/var/www/certbot" \
    nginx:1.27-alpine \
    sh -c 'printf "events{}\nhttp{server{listen 80;location /.well-known/acme-challenge/{root /var/www/certbot;}location /{return 404;}}}\n" > /etc/nginx/nginx.conf && exec nginx -g "daemon off;"'
  sleep 3
  issue
  docker rm -f nginx-acme
  echo ""
  echo "Cert emitido. Suba a stack + edge:"
  echo "  docker compose -f docker-compose.homolog.yml --env-file /opt/forzion/.env up -d"
  echo "  bash scripts/reload-edge.sh"
fi

echo "OK — cert de: $*"
