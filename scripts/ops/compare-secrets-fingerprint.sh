#!/usr/bin/env bash
# Compara o fingerprint (sha256) de 5 segredos entre dois .env, sem nunca imprimir valor
# cru — evita criar o vazamento que a verificação existe pra prevenir (specification-security
# §10.2/§8: conformidade dos valores nos .env da VM não é verificável de fora).
#
# Uso: scripts/ops/compare-secrets-fingerprint.sh <env-a> <env-b>
#   ssh ubuntu@<vm> 'bash -s' < scripts/ops/compare-secrets-fingerprint.sh /opt/forzion/prod/.env /opt/forzion/.env
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Uso: $0 <env-a> <env-b>" >&2
  exit 1
fi

ENV_A="$1"
ENV_B="$2"
CHAVES=(JWT_SECRET MFA_ENCRYPTION_KEY DATA_PROTECTION_KEY INTERNAL_API_KEY DELIVERY_LOG_HASH_KEY)

extrair_valor() {
  local arquivo="$1" chave="$2"
  grep -E "^${chave}=" "$arquivo" | tail -1 | cut -d'=' -f2-
}

printf '%-24s %-64s %-64s %s\n' "CHAVE" "FINGERPRINT_A" "FINGERPRINT_B" "IGUAL?"
for chave in "${CHAVES[@]}"; do
  valor_a="$(extrair_valor "$ENV_A" "$chave")"
  valor_b="$(extrair_valor "$ENV_B" "$chave")"
  hash_a="$(printf '%s' "$valor_a" | sha256sum | cut -d' ' -f1)"
  hash_b="$(printf '%s' "$valor_b" | sha256sum | cut -d' ' -f1)"
  igual="NAO"
  [ "$hash_a" = "$hash_b" ] && igual="SIM -- REUSO DETECTADO"
  printf '%-24s %-64s %-64s %s\n' "$chave" "$hash_a" "$hash_b" "$igual"
done
