#!/usr/bin/env bash
# Guarda 3 invariantes de supply-chain gate que, se regredirem silenciosamente, deixam
# PR pra main sem SAST bloqueante ou reabrem o histórico git ao scan de segredo:
# (1) semgrep.yml dispara em PR pra main com o MESMO job name que main exige como
#     required status check (renomear o job sem atualizar branch protection trava todo
#     PR esperando um check que nunca mais reporta); (2) paths-ignore do trigger de PR
#     de semgrep.yml casa com o de ci.yml (senão um PR só-docs fica pendente esperando
#     um check que não dispara); (3) o job gitleaks do CI mantém --source=/repo e
#     --no-git (o scan de histórico completo é one-shot local, não deve virar desculpa
#     pra mudar o job do CI).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WF="$REPO_ROOT/.github/workflows"
SEMGREP="$WF/semgrep.yml"
CI="$WF/ci.yml"

falhas=0

# Extrai o bloco de um trigger top-level (ex.: "  pull_request:") até a próxima chave
# top-level de mesma indentação (2 espaços) ou EOF -- não assume ordem/adjacência de
# sub-chaves dentro do bloco.
extrair_bloco() {
  local arquivo="$1" chave="$2"
  awk -v chave="^  ${chave}:" '
    $0 ~ chave { dentro=1; next }
    dentro && /^  [a-zA-Z]/ { dentro=0 }
    dentro { print }
  ' "$arquivo"
}

semgrep_pr_block="$(extrair_bloco "$SEMGREP" "pull_request")"
ci_pr_block="$(extrair_bloco "$CI" "pull_request")"

semgrep_branches_line="$(grep 'branches:' <<<"$semgrep_pr_block" || true)"
# Token exato via array bash (splitting em [\[\], ]), não substring -- "remaining-x"
# não deve casar "main".
IFS='[], ' read -ra branches_tokens <<<"${semgrep_branches_line#*branches:}"
tem_main=0
for tok in "${branches_tokens[@]}"; do
  [ "$tok" = "main" ] && tem_main=1
done
if [ "$tem_main" -ne 1 ]; then
  echo "FALHOU -- semgrep.yml: pull_request.branches sem token 'main' exato ($semgrep_branches_line)." >&2
  falhas=$((falhas + 1))
fi

job_name="$(grep -A1 '^  semgrep:' "$SEMGREP" | grep 'name:' | sed -E 's/^\s*name:\s*//')"
if [ "$job_name" != "Semgrep scan" ]; then
  echo "FALHOU -- semgrep.yml: job name é '$job_name', esperado 'Semgrep scan' -- precisa bater EXATO com required_status_checks.contexts de main (branch protection não referencia arquivo nenhum, só o nome do check)." >&2
  falhas=$((falhas + 1))
fi

semgrep_ignore_val="$(grep 'paths-ignore:' <<<"$semgrep_pr_block" | sed -E 's/^\s*paths-ignore:\s*//')"
ci_ignore_val="$(grep 'paths-ignore:' <<<"$ci_pr_block" | sed -E 's/^\s*paths-ignore:\s*//')"
if [ "$semgrep_ignore_val" != "$ci_ignore_val" ]; then
  echo "FALHOU -- semgrep.yml pull_request.paths-ignore diverge do de ci.yml (($semgrep_ignore_val) != ($ci_ignore_val)) -- PR só-docs pode ficar pendente esperando um check que não dispara." >&2
  falhas=$((falhas + 1))
fi

gitleaks_block="$(extrair_bloco "$CI" "gitleaks")"
if ! grep -q -- '--source=/repo' <<<"$gitleaks_block" || ! grep -q -- '--no-git' <<<"$gitleaks_block"; then
  echo "FALHOU -- ci.yml: job gitleaks perdeu --source=/repo ou --no-git (o scan de histórico é one-shot local, não deve virar desculpa pra mudar o job do CI)." >&2
  falhas=$((falhas + 1))
fi

[ "$falhas" -eq 0 ] || exit 1
echo "supply-chain-gates-check: OK -- semgrep.yml cobre main com job name/paths-ignore corretos; gitleaks do CI segue --source=/repo --no-git."
