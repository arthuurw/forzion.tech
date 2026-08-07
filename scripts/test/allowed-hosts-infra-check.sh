#!/usr/bin/env bash
# Trava a divergencia entre o AllowedHosts (host filtering do ASP.NET) e os hosts que a INFRA de
# fato usa pra falar com o backend. Producao nascia so com os dominios publicos, entao o
# healthcheck do compose, os 5 crons (/internal/*) e o proxy do frontend -- todos batendo em
# `localhost`/`backend` -- levavam 400 "Invalid Hostname". So aparecia no 1o deploy real de prod.
# Os hosts esperados sao DERIVADOS dos arquivos de infra (URLs na porta 8080), nao hardcodados:
# uma URL nova com host novo passa a ser exigida sozinha. Nao precisa de Docker.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

# Porta 8080 == backend. Pega compose (healthcheck, API_BASE_URL) e workflows (crons /internal).
mapfile -t HOSTS < <(
  grep -ohE 'http://[a-zA-Z0-9._-]+:8080' docker-compose*.yml .github/workflows/*.yml 2>/dev/null \
    | sed -E 's#^http://##; s#:8080$##' | sort -u
)

[ "${#HOSTS[@]}" -ge 2 ] || {
  echo "FALHOU -- so ${#HOSTS[@]} host(s) na porta 8080; o teste viraria vazio (mudou o padrao das URLs?)." >&2
  exit 1
}
echo "hosts que a infra usa pro backend: ${HOSTS[*]}"

# AllowedHosts e lista separada por ';' -- comparar TOKEN exato, senao 'backend' casaria 'backend-prd'.
permite() { # $1 = arquivo appsettings, $2 = host
  local lista
  lista="$(grep -oE '"AllowedHosts"[[:space:]]*:[[:space:]]*"[^"]*"' "$1" | sed -E 's/.*:[[:space:]]*"([^"]*)"/\1/')"
  [ -n "$lista" ] || return 1
  [ "$lista" = "*" ] && return 0
  printf '%s' "$lista" | awk -v alvo="$2" -F';' '{for(i=1;i<=NF;i++) if($i==alvo) exit 0; exit 1}'
}

checa() { # $1 = arquivo appsettings; ecoa o que faltar
  local faltando=()
  for h in "${HOSTS[@]}"; do
    permite "$1" "$h" || faltando+=("$h")
  done
  [ "${#faltando[@]}" -eq 0 ] || { echo "${faltando[*]}"; return 1; }
}

echo "=== cada ambiente de deploy precisa permitir todos eles ==="
falhou=0
for env in Homolog Production; do
  arq="forzion.tech.Api/appsettings.$env.json"
  [ -f "$arq" ] || { echo "FALHOU -- $arq nao existe." >&2; exit 1; }
  if faltam="$(checa "$arq")"; then
    echo "OK -- $env permite todos."
  else
    echo "FALHOU -- $arq nao permite: $faltam" >&2
    falhou=1
  fi
done
[ "$falhou" -eq 0 ] || exit 1

echo "=== mutacao: tirar um host do Production tem que reprovar ==="
TMP="$(mktemp)"
trap 'rm -f "$TMP"' EXIT
sed -E 's/("AllowedHosts"[^"]*")([^"]*)"/\1forzion.tech"/' \
  forzion.tech.Api/appsettings.Production.json > "$TMP"
grep -q '"AllowedHosts"[[:space:]]*:[[:space:]]*"forzion.tech"' "$TMP" \
  || { echo "FALHOU -- a mutacao nao foi aplicada (o JSON mudou de forma?)." >&2; exit 1; }
if checa "$TMP" >/dev/null; then
  echo "FALHOU -- Production so com o dominio publico deveria reprovar." >&2
  exit 1
fi
echo "OK -- a mutacao reprova: o teste discrimina de verdade."

echo "allowed-hosts-infra-check: OK -- AllowedHosts cobre todo host que a infra usa no backend."
