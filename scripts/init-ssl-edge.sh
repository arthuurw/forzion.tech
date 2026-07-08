#!/bin/bash
# Bootstrap do edge (nginx/certbot compartilhado hmg+prd). Executar UMA VEZ,
# na ordem de specs/specification-infrastructure.md §ISOLAMENTO-PRD-HMG.
#
# Uso: bash scripts/init-ssl-edge.sh seu@email.com

set -euo pipefail

EMAIL="${1:?Uso: $0 <email>}"
DIR=/opt/forzion/prod/app

DOMAINS_PROD=(forzion.tech www.forzion.tech app.forzion.tech)
DOMAINS_HMG=(homologacao.forzion.tech pact.homologacao.forzion.tech)

cd "$DIR"

docker network create forzion-edge 2>/dev/null || true

docker compose -p edge -f docker-compose.edge.yml stop nginx 2>/dev/null || true
docker run --rm -d \
  --name nginx-edge-init \
  -p 80:80 \
  -v "$DIR/nginx/nginx-edge-init.conf:/etc/nginx/nginx.conf:ro" \
  -v "$DIR/certbot/www:/var/www/certbot" \
  nginx:1.27-alpine

sleep 3

certbot_flags() { for d in "$@"; do printf -- '-d %s ' "$d"; done; }

docker run --rm \
  -v "$DIR/certbot/conf:/etc/letsencrypt" \
  -v "$DIR/certbot/www:/var/www/certbot" \
  certbot/certbot certonly \
    --webroot --webroot-path=/var/www/certbot \
    --email "$EMAIL" --agree-tos --no-eff-email \
    $(certbot_flags "${DOMAINS_PROD[@]}")

docker run --rm \
  -v "$DIR/certbot/conf:/etc/letsencrypt" \
  -v "$DIR/certbot/www:/var/www/certbot" \
  certbot/certbot certonly \
    --webroot --webroot-path=/var/www/certbot \
    --email "$EMAIL" --agree-tos --no-eff-email \
    $(certbot_flags "${DOMAINS_HMG[@]}")

docker stop nginx-edge-init

echo ""
echo "Certificados ok. Subindo stack edge (nginx.conf real, 2 dominios)..."
docker compose -p edge -f docker-compose.edge.yml up -d

echo "Acesse: https://forzion.tech e https://homologacao.forzion.tech"
