#!/usr/bin/env bash
# Guarda invariantes de higiene que, se regredirem silenciosamente, reabrem gaps já
# fechados: (1) server_tokens off no bloco http do nginx (senão a borda volta a expor a
# versão exata do nginx, superfície de fingerprinting); (2) .gitignore cobre o padrão
# `*;C` de resíduo de redirecionamento do PowerShell (senão o mesmo lixo volta a poluir
# o build/gitleaks a cada sessão Windows); (3) ssl_ciphers só aceita ECDHE+AEAD (nunca
# RSA-kex sem forward secrecy, nunca CBC/SHA1) e ssl_prefer_server_ciphers está ligado.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
NGINX_CONF="$REPO_ROOT/nginx/nginx.conf"
GITIGNORE="$REPO_ROOT/.gitignore"

falhas=0

if ! grep -qE '^\s*server_tokens\s+off\s*;' "$NGINX_CONF"; then
  echo "FALHOU -- nginx/nginx.conf: 'server_tokens off;' ausente -- borda volta a expor a versão exata do nginx." >&2
  falhas=$((falhas + 1))
fi

if ! grep -qxF '*;C' "$GITIGNORE"; then
  echo "FALHOU -- .gitignore: padrão '*;C' ausente -- resíduo de redirecionamento do PowerShell volta a poluir o working tree." >&2
  falhas=$((falhas + 1))
fi

cipher_lines="$(grep -E '^\s*ssl_ciphers\s' "$NGINX_CONF" || true)"
if [ -z "$cipher_lines" ]; then
  echo "FALHOU -- nginx/nginx.conf: nenhuma diretiva ssl_ciphers encontrada." >&2
  falhas=$((falhas + 1))
else
  sem_ecdhe=0
  sem_aead=0
  while IFS= read -r line; do
    valor="$(printf '%s' "$line" | sed -E 's/^[[:space:]]*ssl_ciphers[[:space:]]+//; s/;[[:space:]]*$//')"
    old_ifs="$IFS"
    IFS=':'
    for suite in $valor; do
      case "$suite" in
        ECDHE-*) ;;
        *) sem_ecdhe=1 ;;
      esac
      case "$suite" in
        *-GCM-*|*-POLY1305|*-CCM|*-CCM8) ;;
        *) sem_aead=1 ;;
      esac
    done
    IFS="$old_ifs"
  done <<<"$cipher_lines"

  if [ "$sem_ecdhe" -eq 1 ]; then
    echo "FALHOU -- nginx/nginx.conf: ssl_ciphers aceita suite sem ECDHE (RSA-kex ou DHE clássico) -- perde forward secrecy garantida." >&2
    falhas=$((falhas + 1))
  fi
  if [ "$sem_aead" -eq 1 ]; then
    echo "FALHOU -- nginx/nginx.conf: ssl_ciphers aceita suite CBC/SHA1 (não-AEAD)." >&2
    falhas=$((falhas + 1))
  fi
fi

if ! grep -qE '^\s*ssl_prefer_server_ciphers\s+on\s*;' "$NGINX_CONF"; then
  echo "FALHOU -- nginx/nginx.conf: 'ssl_prefer_server_ciphers on;' ausente." >&2
  falhas=$((falhas + 1))
fi

[ "$falhas" -eq 0 ] || exit 1
echo "nginx-hygiene-check: OK -- server_tokens off presente; .gitignore cobre *;C; ssl_ciphers ECDHE+AEAD; ssl_prefer_server_ciphers on."
