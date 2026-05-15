#!/bin/bash
# Obtém o primeiro certificado SSL via Let's Encrypt.
# Executar UMA VEZ após DNS propagado e setup-vm.sh concluído.
#
# Uso: bash scripts/init-ssl.sh homolog.forzion.tech seu@email.com

set -euo pipefail

DOMAIN="${1:?Uso: $0 <dominio> <email>}"
EMAIL="${2:?Uso: $0 <dominio> <email>}"
DIR=/opt/forzion

cd "$DIR"

# Nginx com config HTTP-only para passar no desafio ACME
cp nginx/nginx-init.conf nginx/nginx-active.conf

docker compose -f docker-compose.server.yml stop nginx 2>/dev/null || true
docker run --rm -d \
  --name nginx-init \
  -p 80:80 \
  -v "$DIR/nginx/nginx-init.conf:/etc/nginx/nginx.conf:ro" \
  -v "$DIR/certbot/www:/var/www/certbot" \
  nginx:1.27-alpine

sleep 3

# Obter certificado
docker run --rm \
  -v "$DIR/certbot/conf:/etc/letsencrypt" \
  -v "$DIR/certbot/www:/var/www/certbot" \
  certbot/certbot certonly \
    --webroot \
    --webroot-path=/var/www/certbot \
    --email "$EMAIL" \
    --agree-tos \
    --no-eff-email \
    -d "$DOMAIN"

docker stop nginx-init

# Substituir placeholder pelo domínio real no nginx.conf
sed -i "s/DOMAIN_PLACEHOLDER/$DOMAIN/g" "$DIR/nginx/nginx.conf"

echo ""
echo "✅  Certificado obtido. Iniciando stack completa..."
docker compose -f docker-compose.server.yml --env-file .env up -d

echo "✅  Acesse: https://$DOMAIN"
