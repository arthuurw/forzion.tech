#!/usr/bin/env bash
# Probe ASSINADO da borda de agentes (AGF0-37). O probe do deploy exige 401 e prova só a ROTA;
# este exige 200 e prova a ASSINATURA — payload canônico, janela de timestamp e segredo, que é
# o que o gateway externo vai exercitar de verdade.
#
# Roda DENTRO da VM: o segredo é root-only no .env e nunca é impresso, nem passado por argv
# (fica em variável de ambiente lida pelo python3) — argv é visível a qualquer usuário via ps.
#
# Uso: scripts/ops/agents-signed-probe.sh [env-file] [porta] [caminho]
#   ssh ubuntu@<vm> 'sudo bash -s' < scripts/ops/agents-signed-probe.sh
#   ssh ubuntu@<vm> 'sudo bash -s' < scripts/ops/agents-signed-probe.sh /opt/forzion/prod/.env 8444
set -euo pipefail

ENV_FILE="${1:-/opt/forzion/.env}"
PORTA="${2:-8443}"
CAMINHO="${3:-/internal/agents/v1/health}"
HOST_TAILNET="${HOST_TAILNET:-100.114.212.86}"

if [ ! -r "$ENV_FILE" ]; then
  echo "ERRO: $ENV_FILE inexistente ou sem permissão de leitura (rodar com sudo)." >&2
  exit 1
fi

command -v python3 >/dev/null || { echo "ERRO: python3 ausente." >&2; exit 1; }

AGENTS_HMAC_SECRET_ATUAL="$(sed -n 's/^AGENTS_HMAC_SECRET_ATUAL=//p' "$ENV_FILE" | head -1)"
AGENTS_HMAC_SECRET_ATUAL="${AGENTS_HMAC_SECRET_ATUAL%\"}"
AGENTS_HMAC_SECRET_ATUAL="${AGENTS_HMAC_SECRET_ATUAL#\"}"
export AGENTS_HMAC_SECRET_ATUAL

if [ -z "$AGENTS_HMAC_SECRET_ATUAL" ]; then
  echo "ERRO: AGENTS_HMAC_SECRET_ATUAL ausente em $ENV_FILE." >&2
  exit 1
fi

TS="$(date -u +%s)"

# O payload NÃO tem \n final e o timestamp entra na forma decimal mínima — o backend re-serializa
# o header com long.ToString(), então zero à esquerda faria os dois lados assinarem strings
# diferentes e o 401 sairia mudo.
ASSINATURA="$(
  CAMINHO="$CAMINHO" TS="$TS" python3 - <<'PY'
import hashlib, hmac, os

segredo = os.environ["AGENTS_HMAC_SECRET_ATUAL"].encode()
corpo_hash = hashlib.sha256(b"").hexdigest()
payload = "\n".join(["GET", os.environ["CAMINHO"], corpo_hash, str(int(os.environ["TS"]))])
print(hmac.new(segredo, payload.encode(), hashlib.sha256).hexdigest())
PY
)"

RESPOSTA="$(mktemp)"
trap 'rm -f "$RESPOSTA"' EXIT

CODIGO="$(curl -s -o "$RESPOSTA" -w '%{http_code}' -m 10 \
  -H "X-Forzion-Timestamp: $TS" \
  -H "X-Forzion-Signature: v1=$ASSINATURA" \
  "http://${HOST_TAILNET}:${PORTA}${CAMINHO}" || true)"

CODE_WIRE="$(sed -n 's/.*"code" *: *"\([a-z_]*\)".*/\1/p' "$RESPOSTA" | head -1)"

echo "alvo: http://${HOST_TAILNET}:${PORTA}${CAMINHO} · ts=${TS} · HTTP ${CODIGO}${CODE_WIRE:+ · code=$CODE_WIRE}"

case "$CODIGO" in
  200) echo "OK -- assinatura aceita. AGF0-37 fechado para esta porta."; exit 0 ;;
  000) echo "FALHA: sem resposta -- porta nao publicada, tailscaled fora ou ACL ausente." >&2; exit 1 ;;
  404) echo "FALHA: 404 -- location nao casou, ou caiu na borda publica." >&2; exit 1 ;;
  502|504) echo "FALHA: $CODIGO -- rota certa, backend do ambiente fora." >&2; exit 1 ;;
  503) echo "FALHA: 503 -- assinatura ACEITA, mas a tag agents-ready esta Unhealthy (db/schema)." >&2; exit 1 ;;
  401)
    case "$CODE_WIRE" in
      timestamp_out_of_window) echo "FALHA: assinatura CONFERE, relogio fora da janela de 300s -- corrigir o relogio, nao o segredo." >&2 ;;
      *) echo "FALHA: 401 signature_invalid -- segredo divergente do .env deste ambiente, ou payload canonico divergente (proxy_pass sem \$request_uri renormaliza o caminho)." >&2 ;;
    esac
    exit 1 ;;
  *) echo "FALHA: HTTP $CODIGO inesperado." >&2; exit 1 ;;
esac
