#!/bin/bash
# Executar UMA VEZ na VM OCI após criação.
# ssh ubuntu@<IP> 'bash -s' < scripts/setup-vm.sh

set -euo pipefail

# --- Docker ---
sudo apt-get update -qq
sudo apt-get install -y -qq ca-certificates curl gnupg
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update -qq
sudo apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-compose-plugin
sudo usermod -aG docker "$USER"

# --- Diretório da aplicação ---
sudo mkdir -p /opt/forzion/nginx
sudo mkdir -p /opt/forzion/certbot/conf
sudo mkdir -p /opt/forzion/certbot/www
sudo chown -R "$USER:$USER" /opt/forzion

# --- .env (preencher após execução) ---
if [ ! -f /opt/forzion/.env ]; then
  cat > /opt/forzion/.env <<'EOF'
REGISTRY=<região>.ocir.io/<namespace>
TAG=latest
APP_ENV=Homolog
DB_CONNECTION=Host=db.xxx.supabase.co;Database=postgres;Username=forzion_api;Password=SENHA;SSL Mode=Require;Trust Server Certificate=true;Search Path=homolog
DB_SCHEMA=homolog
JWT_SECRET=TROQUE_POR_SECRET_FORTE_MINIMO_32_CHARS
JWT_ISSUER=forzion.tech
JWT_AUDIENCE=forzion.tech
CORS_ORIGINS=https://homolog.forzion.tech
EOF
  echo ""
  echo "⚠️  Edite /opt/forzion/.env com os valores reais antes de continuar."
fi

echo ""
echo "✅  VM configurada. Próximos passos:"
echo "   1. Edite /opt/forzion/.env"
echo "   2. Configure DNS: homolog.forzion.tech → $(curl -s ifconfig.me)"
echo "   3. Execute: bash /opt/forzion/scripts/init-ssl.sh homolog.forzion.tech seu@email.com"
echo "   4. Faça logout e login novamente (grupo docker)"
