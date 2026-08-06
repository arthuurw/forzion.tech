#!/usr/bin/env bash
# Extrai o guard do alvo de prod do db-backup.yml e roda as 3 combinações. O que importa é o
# caso do meio: secret ausente COM o deploy de prod armado tem que ser vermelho — pular em
# silêncio ali significa produção real rodando sem nenhum backup.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

awk '/# prod \(forzion_p\)/,/^          fi$/' "$REPO_ROOT/.github/workflows/db-backup.yml" \
  | sed 's/^          //' > "$WORK/guard.sh"

grep -q 'backup_one' "$WORK/guard.sh" || { echo "FALHOU -- guard nao extraido do YAML." >&2; exit 1; }

roda() {
  BACKUP_DATABASE_URL_PROD="$1" PROD_DEPLOY_ENABLED="$2" bash -c '
    set -euo pipefail
    backup_one() { echo "BACKUP_ONE schemas=$2 prefixo=$3"; }
    source "$0"
  ' "$WORK/guard.sh" 2>&1
}

echo "=== (a) secret presente -> dump de prod ==="
saida="$(roda "postgres://prod" "true")"
grep -q "BACKUP_ONE schemas=public prefixo=prod/" <<<"$saida" \
  || { echo "FALHOU -- devia dumpar prod. Saida: $saida" >&2; exit 1; }
echo "OK -- alvo de prod executado."

echo "=== (b) secret ausente + PROD_DEPLOY_ENABLED=true -> falha vermelha ==="
if saida="$(roda "" "true")"; then
  echo "FALHOU -- devia sair != 0 com prod armado e sem secret. Saida: $saida" >&2
  exit 1
fi
grep -q "::error::" <<<"$saida" || { echo "FALHOU -- sem ::error:: na saida: $saida" >&2; exit 1; }
echo "OK -- prod armado sem backup reprova o run."

echo "=== (c) secret ausente + prod não armado -> no-op verde ==="
saida="$(roda "" "")"
grep -q "backup prod pulado" <<<"$saida" || { echo "FALHOU -- devia pular. Saida: $saida" >&2; exit 1; }
grep -q "BACKUP_ONE" <<<"$saida" && { echo "FALHOU -- nao devia dumpar prod. Saida: $saida" >&2; exit 1; }
echo "OK -- pré-go-live segue verde sem alarme falso."

echo "db-backup-prod-guard-check: OK -- guard cobre as 3 combinacoes."
