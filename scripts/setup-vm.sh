#!/bin/bash
# Executar UMA VEZ na VPS (Hostinger, Ubuntu) após criação.
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

# --- Limpeza automática do store Docker (timer systemd semanal) ---
if [ -f /opt/forzion/app/scripts/systemd/forzion-docker-prune.timer ]; then
  sudo cp /opt/forzion/app/scripts/systemd/forzion-docker-prune.service /etc/systemd/system/
  sudo cp /opt/forzion/app/scripts/systemd/forzion-docker-prune.timer /etc/systemd/system/
  sudo systemctl daemon-reload
  sudo systemctl enable --now forzion-docker-prune.timer
fi

# --- fail2ban (brute-force no edge: bane IP após N falhas de auth/rate-limit) ---
sudo apt-get install -y -qq fail2ban
sudo mkdir -p /var/log/forzion-nginx
if [ -f /opt/forzion/app/infra/fail2ban/filter.d/forzion-nginx-auth.conf ]; then
  sudo cp /opt/forzion/app/infra/fail2ban/filter.d/forzion-nginx-auth.conf /etc/fail2ban/filter.d/
  sudo cp /opt/forzion/app/infra/fail2ban/jail.d/forzion.local /etc/fail2ban/jail.d/
  sudo systemctl enable --now fail2ban
  sudo systemctl restart fail2ban
fi

# --- Firewall (ufw) ---
if [ -f /opt/forzion/app/scripts/setup-firewall.sh ]; then
  bash /opt/forzion/app/scripts/setup-firewall.sh
fi

# --- .env (preencher após execução) ---
if [ ! -f /opt/forzion/.env ]; then
  cat > /opt/forzion/.env <<'EOF'
APP_ENV=Homolog
# DB_CONNECTION: Session pooler Supabase (porta 5432, IPv4) — copiar host/user EXATOS do
# Dashboard > Connect > Session pooler (user = forzion_api.<project-ref>). NÃO usar Transaction
# pooler :6543 (sem prepared stmt/session var → quebra MigrateAsync no boot). Direct
# db.<ref>.supabase.co é IPv6-only (inacessível de alguns hosts/containers).
DB_CONNECTION=Host=aws-0-<regiao>.pooler.supabase.com;Port=5432;Database=postgres;Username=forzion_api.<project-ref>;Password=SENHA;SSL Mode=Require;Trust Server Certificate=true;Search Path=homolog
DB_SCHEMA=homolog
JWT_SECRET=TROQUE_POR_SECRET_FORTE_MINIMO_32_CHARS
JWT_ISSUER=homologacao.forzion.tech
JWT_AUDIENCE=homologacao.forzion.tech
CORS_ORIGINS=https://homologacao.forzion.tech
STRIPE_SECRET_KEY=
STRIPE_WEBHOOK_SECRET=
STRIPE_URL_BASE=https://homologacao.forzion.tech
SEED_ZAP_TEST_PASSWORD=
EOF
  echo ""
  echo "⚠️  Edite /opt/forzion/.env com os valores reais antes de continuar."
fi

echo ""
echo "✅  VM configurada. Próximos passos:"
echo "   1. Edite /opt/forzion/.env"
echo "   2. Configure DNS: homolog.forzion.tech → $(curl -s ifconfig.me)"
echo "   3. Execute: bash /opt/forzion/scripts/init-ssl.sh homologacao.forzion.tech seu@email.com"
echo "   4. Faça logout e login novamente (grupo docker)"
