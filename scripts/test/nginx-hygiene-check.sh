#!/usr/bin/env bash
# Guarda 2 invariantes de higiene que, se regredirem silenciosamente, reabrem gaps já
# fechados: (1) server_tokens off no bloco http do nginx (senão a borda volta a expor a
# versão exata do nginx, superfície de fingerprinting); (2) .gitignore cobre o padrão
# `*;C` de resíduo de redirecionamento do PowerShell (senão o mesmo lixo volta a poluir
# o build/gitleaks a cada sessão Windows).
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

[ "$falhas" -eq 0 ] || exit 1
echo "nginx-hygiene-check: OK -- server_tokens off presente; .gitignore cobre *;C."
