#!/usr/bin/env bash
# Guarda 4 invariantes de CI/CD que, se regredirem silenciosamente, reabrem gaps já
# fechados na auditoria: (1) todo workflow declara `permissions:` no topo (escopo mínimo
# do GITHUB_TOKEN); (2) as imagens de gate de segurança seguem pinadas por digest, nunca
# em tag móvel; (3) o schedule do ZAP continua resolvendo o modo full (sem isso,
# `github.event.inputs.mode` vazio no schedule faz o baseline vencer sempre); (4) o
# Renovate continua mirando `homolog`, nunca o default branch `main` (produção).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WORKFLOWS_DIR="$REPO_ROOT/.github/workflows"
ZAP_YML="$WORKFLOWS_DIR/zap.yml"
RENOVATE_JSON="$REPO_ROOT/renovate.json"

falhas=0

# --- 1. permissions: no topo de todo workflow -------------------------------------
sem_permissions=()
while IFS= read -r -d '' f; do
  if ! grep -qE '^permissions:' "$f"; then
    sem_permissions+=("$(basename "$f")")
  fi
done < <(find "$WORKFLOWS_DIR" -maxdepth 1 -name '*.yml' -print0)

if [ "${#sem_permissions[@]}" -gt 0 ]; then
  echo "FALHOU -- workflow(s) sem 'permissions:' no topo: ${sem_permissions[*]}" >&2
  falhas=$((falhas + 1))
fi

# --- 2. imagens de gate pinadas por digest (nunca tag móvel) ------------------------
# "arquivo:imagem" -- cada imagem tem de aparecer como imagem@sha256:... no arquivo, e
# nunca mais como imagem:latest / imagem:stable (tags móveis que escondem drift de versão).
gate_images=(
  "$WORKFLOWS_DIR/semgrep.yml:semgrep/semgrep"
  "$WORKFLOWS_DIR/zap.yml:ghcr.io/zaproxy/zaproxy"
  "$WORKFLOWS_DIR/ci.yml:ghcr.io/zaproxy/zaproxy"
  "$REPO_ROOT/docker-compose.homolog.yml:pactfoundation/pact-broker"
  "$REPO_ROOT/docker-compose.edge.yml:certbot/certbot"
)

for entry in "${gate_images[@]}"; do
  file="${entry%%:*}"
  # a imagem pode conter ':' (registries com porta não usados aqui, mas por robustez
  # extrai tudo após o primeiro ':').
  imagem="${entry#*:}"
  imagem_regex="$(printf '%s' "$imagem" | sed -E 's/[.]/\\./g')"

  if ! grep -qE "${imagem_regex}@sha256:[0-9a-f]{64}" "$file"; then
    echo "FALHOU -- $(basename "$file"): imagem '$imagem' não está pinada por @sha256:<digest>." >&2
    falhas=$((falhas + 1))
  fi
  if grep -qE "${imagem_regex}:(latest|stable)([^0-9a-zA-Z._-]|\$)" "$file"; then
    echo "FALHOU -- $(basename "$file"): imagem '$imagem' voltou a usar tag móvel (latest/stable)." >&2
    falhas=$((falhas + 1))
  fi
done

# --- 3. ZAP: schedule resolve para o full autenticado -------------------------------
if [ ! -f "$ZAP_YML" ]; then
  echo "FALHOU -- $ZAP_YML não encontrado." >&2
  falhas=$((falhas + 1))
elif ! grep -q "github.event_name == 'schedule'" "$ZAP_YML"; then
  echo "FALHOU -- zap.yml: resolução de modo sensível a 'schedule' ausente -- o cron semanal volta a rodar só o baseline." >&2
  falhas=$((falhas + 1))
elif grep -qE "if:\s*\\\$\{\{\s*github\.event\.inputs\.mode\s*(==|!=)\s*'full'\s*\}\}" "$ZAP_YML"; then
  echo "FALHOU -- zap.yml: step ainda gateia direto por github.event.inputs.mode (regressão do bug original -- vazio no schedule)." >&2
  falhas=$((falhas + 1))
fi

# --- 4. Renovate mira homolog, nunca main --------------------------------------------
if [ ! -f "$RENOVATE_JSON" ]; then
  echo "FALHOU -- $RENOVATE_JSON não encontrado." >&2
  falhas=$((falhas + 1))
elif ! grep -qE '"baseBranch(es|Patterns)"\s*:\s*\[\s*"homolog"\s*\]' "$RENOVATE_JSON"; then
  echo "FALHOU -- renovate.json: baseBranches/baseBranchPatterns não fixa 'homolog' -- PRs automáticos podem nascer contra main (produção)." >&2
  falhas=$((falhas + 1))
fi

[ "$falhas" -eq 0 ] || exit 1
echo "ci-hygiene-check: OK -- permissions em todo workflow; imagens de gate pinadas; zap.yml resolve full no schedule; renovate mira homolog."
