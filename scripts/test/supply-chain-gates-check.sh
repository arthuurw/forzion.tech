#!/usr/bin/env bash
# secprod-supply-chain-gates (M6): garante que semgrep.yml dispara em PR pra main (senao
# o 2o required check fica sem cobertura real) e que seu paths-ignore continua identico ao
# de ci.yml (Edge Case do spec: paths-ignore divergente deixaria PR so-docs pendente
# esperando um check que nunca dispara). Tambem guarda o --no-git do job gitleaks do CI --
# o scan de historico (gitleaks-historico.md) e one-shot local, o job do CI continua
# so-arvore-de-trabalho por decisao (AC-P3.4); se sumir aqui, alguem tirou por engano.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WF="$REPO_ROOT/.github/workflows"
SEMGREP="$WF/semgrep.yml"
CI="$WF/ci.yml"

falhas=0

pr_branches="$(grep -A1 '^  pull_request:' "$SEMGREP" | grep 'branches:')"
case "$pr_branches" in
  *main*) ;;
  *) echo "FALHOU -- semgrep.yml: pull_request.branches sem 'main' ($pr_branches)." >&2
     falhas=$((falhas + 1)) ;;
esac

semgrep_ignore="$(grep 'paths-ignore:' "$SEMGREP")"
ci_ignore="$(grep 'paths-ignore:' "$CI" | head -1)"
semgrep_ignore_val="${semgrep_ignore#*paths-ignore:}"
ci_ignore_val="${ci_ignore#*paths-ignore:}"
if [ "$semgrep_ignore_val" != "$ci_ignore_val" ]; then
  echo "FALHOU -- semgrep.yml paths-ignore diverge de ci.yml (($semgrep_ignore_val) != ($ci_ignore_val)) -- PR só-docs pode ficar pendente esperando um check que não dispara." >&2
  falhas=$((falhas + 1))
fi

if ! grep -qE '^\s*detect --source=/repo --no-git' "$CI"; then
  echo "FALHOU -- ci.yml: job gitleaks perdeu --no-git (o scan de histórico é one-shot local, não deve virar desculpa pra mudar o job do CI)." >&2
  falhas=$((falhas + 1))
fi

[ "$falhas" -eq 0 ] || exit 1
echo "supply-chain-gates-check: OK -- semgrep.yml cobre main com paths-ignore simétrico; gitleaks do CI segue --no-git."
