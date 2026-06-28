#!/bin/bash
# Firewall (ufw) — idempotente. Rodável sozinho na VM viva OU chamado por setup-vm.sh.
# ssh ubuntu@<IP> 'bash -s' < scripts/setup-firewall.sh
# Rationale (allows antes do enable, --force, 22 pública, gotcha DOCKER-USER): specification-infrastructure §FIREWALL.

set -euo pipefail

sudo apt-get install -y -qq ufw
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw --force enable
sudo ufw status verbose
