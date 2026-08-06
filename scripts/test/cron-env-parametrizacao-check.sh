#!/usr/bin/env bash
# Todo cron que roda no matrix de CRON_ENVIRONMENTS tem que resolver chave SSH, APP_DIR e
# ENV_FILE pelo ambiente — um literal de homolog só aparece quando o job JÁ está rodando
# contra production (secret inexistente no env `production` = falha em cima de dado real).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WF="$REPO_ROOT/.github/workflows"

mapfile -t CRONS < <(grep -l "vars.CRON_ENVIRONMENTS" "$WF"/*.yml | sort)
[ "${#CRONS[@]}" -ge 5 ] || { echo "FALHOU -- só ${#CRONS[@]} workflow(s) com CRON_ENVIRONMENTS; padrão de busca quebrou." >&2; exit 1; }

falhas=0
for wf in "${CRONS[@]}"; do
  nome="$(basename "$wf")"

  if grep -qE '^\s*key: \$\{\{ secrets\.HOMOLOG_SSH_KEY \}\}\s*$' "$wf"; then
    echo "FALHOU -- $nome: chave SSH fixa em HOMOLOG_SSH_KEY (esperado ternário por matrix.env)." >&2
    falhas=$((falhas + 1))
  fi
  if grep -qE '^\s*cd /opt/forzion/app\s*$' "$wf"; then
    echo "FALHOU -- $nome: APP_DIR hardcoded (esperado vars.APP_DIR com fallback)." >&2
    falhas=$((falhas + 1))
  fi
  if grep -qE '^\s*EF=/opt/forzion/\.env\s*$' "$wf"; then
    echo "FALHOU -- $nome: ENV_FILE hardcoded (esperado vars.ENV_FILE com fallback)." >&2
    falhas=$((falhas + 1))
  fi
done

[ "$falhas" -eq 0 ] || exit 1
echo "cron-env-parametrizacao-check: OK -- ${#CRONS[@]} crons resolvem chave/APP_DIR/ENV_FILE por ambiente."
