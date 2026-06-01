#!/usr/bin/env bash
# scripts/setup-git.sh — aplica configs git recomendadas para este repo (solo dev).
# Idempotente: re-executar é seguro. Reflete o que está documentado em
# specs/specification-git.md.
#
# Uso:
#   ./scripts/setup-git.sh            # aplica em --global (default)
#   ./scripts/setup-git.sh --local    # aplica em --local (só este clone)
#
# Após rodar, valide:
#   git config --get push.autoSetupRemote   # → true
#   git config --get push.default           # → current

set -euo pipefail

SCOPE="--global"
if [ "${1:-}" = "--local" ]; then
    SCOPE="--local"
fi

# core.autocrlf=input em POSIX (vs "true" no Windows). Normaliza pra LF no commit
# mas preserva LF no checkout — alinha com o pre-commit dotnet format ENDOFLINE
# sem reescrever os arquivos para CRLF localmente.
declare -a CONFIGS=(
    "init.defaultBranch=main"
    "push.autoSetupRemote=true"
    "push.default=current"
    "pull.rebase=true"
    "rebase.autoStash=true"
    "fetch.prune=true"
    "core.autocrlf=input"
)

echo "Aplicando configs git em $SCOPE..."
echo

for cfg in "${CONFIGS[@]}"; do
    key="${cfg%%=*}"
    value="${cfg#*=}"
    git config "$SCOPE" "$key" "$value"
    echo "[OK] $key = $value"
done

echo
echo "Configs aplicadas. Veja specs/specification-git.md para racional e troubleshooting."
echo
echo "Validação rápida:"
echo "  git config --get push.autoSetupRemote"
echo "  git config --get push.default"
echo "  git config --get pull.rebase"
