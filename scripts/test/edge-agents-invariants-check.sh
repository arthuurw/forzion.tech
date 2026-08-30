#!/usr/bin/env bash
# Gate estatico dos listeners do tailnet (gateway de agentes) em nginx/nginx.conf e
# docker-compose.edge.yml. NAO precisa de Docker -- roda no job de higiene do CI, ao
# contrario dos repro edge-*-check.sh, que exigem daemon e por isso so rodam a mao.
#
# Cobre a classe de erro que `nginx -t` aprova e o deploy nao pega:
#   - proxy_pass com URI de destino renormaliza o caminho => TODA requisicao assinada
#     morre em 401 mudo (o backend assina sobre o RawTarget da wire);
#   - porta publicada sem IP => 8443/8444 na internet, e o ufw NAO filtra porta do Docker;
#   - `return 404` de /internal/ removido de um vhost publico => API interna exposta.
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONF="$RAIZ/nginx/nginx.conf"
COMPOSE="$RAIZ/docker-compose.edge.yml"
IP_TAILNET="100.114.212.86"

falhou=0
ok()     { echo "OK -- $1"; }
reprova() { echo "edge-agents-invariants-check: FALHOU -- $1" >&2; falhou=1; }

for arquivo in "$CONF" "$COMPOSE"; do
  [ -f "$arquivo" ] || { echo "arquivo ausente: $arquivo" >&2; exit 1; }
done

# O repo guarda LF, mas `text=auto` entrega CRLF no working tree do Windows. Sem tirar o CR
# as ancoras `$` dependeriam de qual grep/sed esta instalado -- o gate tem que dar o mesmo
# veredito no CI Linux e na maquina de quem edita.
CONF_TXT="$(tr -d '\r' < "$CONF")"
COMPOSE_TXT="$(tr -d '\r' < "$COMPOSE")"

# Recorta os `server` blocks que contem `listen 8443|8444`. Ancorado na indentacao de 4
# espacos do arquivo: reformatacao derruba o recorte e o check REPROVA (caso 1) em vez de
# passar vazio -- gate que nao acha o alvo tem que gritar, nao silenciar.
blocos_agentes() {
  awk '
    /^    server \{/ { bloco = ""; dentro = 1 }
    dentro          { bloco = bloco $0 "\n" }
    /^    \}$/ && dentro {
      dentro = 0
      if (bloco ~ /listen (8443|8444);/) printf "%s", bloco
    }
  ' <<< "$CONF_TXT"
}

AGENTES="$(blocos_agentes)"

echo "=== 1: existem exatamente 2 server blocks de agentes (8443 hmg, 8444 prd) ==="
qtd_listen="$(printf '%s' "$AGENTES" | grep -cE '^        listen (8443|8444);$' || true)"
if [ "$qtd_listen" = "2" ] \
  && printf '%s' "$AGENTES" | grep -qE '^        listen 8443;$' \
  && printf '%s' "$AGENTES" | grep -qE '^        listen 8444;$'; then
  ok "8443 e 8444 declaradas, uma vez cada."
else
  reprova "esperava 1 listen 8443 e 1 listen 8444 nos blocos de agentes, achei $qtd_listen listen(s)."
fi

echo "=== 2: proxy_pass dos blocos de agentes preserva \$request_uri cru ==="
qtd_pp="$(printf '%s' "$AGENTES" | grep -cE '^ +proxy_pass ' || true)"
qtd_pp_ok="$(printf '%s' "$AGENTES" | grep -cE '^ +proxy_pass  *http://\$[a-z_]+:8080\$request_uri;$' || true)"
if [ "$qtd_pp" = "2" ] && [ "$qtd_pp_ok" = "2" ]; then
  ok "os 2 proxy_pass usam upstream por variavel e terminam em \$request_uri."
else
  reprova "proxy_pass fora do padrao http://\$var:8080\$request_uri; ($qtd_pp_ok de $qtd_pp conformes)."
fi

echo "=== 3: os blocos so servem /internal/agents/v1/ e devolvem 404 no resto ==="
qtd_loc="$(printf '%s' "$AGENTES" | grep -c '^        location /internal/agents/v1/ {$' || true)"
qtd_catchall="$(printf '%s' "$AGENTES" | grep -c '^            return 404;$' || true)"
if [ "$qtd_loc" = "2" ] && [ "$qtd_catchall" = "2" ]; then
  ok "cada bloco tem o prefixo de agentes e um catch-all 404."
else
  reprova "esperava 2 location /internal/agents/v1/ e 2 return 404, achei $qtd_loc e $qtd_catchall."
fi

echo "=== 4: os blocos nao tem TLS nem server_name ==="
if printf '%s' "$AGENTES" | grep -qE '(ssl|server_name)'; then
  reprova "bloco de agentes com ssl/server_name -- IP literal nao manda SNI e nao ha hostname confiavel aqui."
else
  ok "sem ssl e sem server_name, como o desenho exige."
fi

echo "=== 5: os vhosts publicos mantem o 404 de /internal/ ==="
qtd_404_publico="$(grep -A2 '^        location /internal/ {$' <<< "$CONF_TXT" | grep -c '^            return 404;$' || true)"
if [ "$qtd_404_publico" = "2" ]; then
  ok "os 2 vhosts publicos seguem fechando /internal/ com 404."
else
  reprova "esperava 2 vhosts publicos com return 404 em /internal/, achei $qtd_404_publico."
fi

echo "=== 6: toda porta publicada alem de 80/443 e ligada a um IP ==="
portas="$(awk '/^    ports:$/ { dentro = 1; next } dentro && /^      - / { print; next } dentro { dentro = 0 }' <<< "$COMPOSE_TXT" \
  | sed -E 's/^      - "//; s/"$//')"
sem_ip=0
for porta in $portas; do
  case "$porta" in
    80:80|443:443) continue ;;
    *:*:*) continue ;;
    *) reprova "porta publicada sem IP: \"$porta\" -- o ufw nao filtra porta do Docker."; sem_ip=1 ;;
  esac
done
[ "$sem_ip" = 0 ] && ok "nenhuma porta nua alem de 80/443."

echo "=== 7: 8443 e 8444 publicadas no IP do tailnet ==="
for p in 8443 8444; do
  if printf '%s\n' "$portas" | grep -qx "$IP_TAILNET:$p:$p"; then
    ok "$p ligada a $IP_TAILNET."
  else
    reprova "$p nao esta publicada como $IP_TAILNET:$p:$p."
  fi
done

[ "$falhou" = 0 ] || exit 1
echo "edge-agents-invariants-check: OK -- rota do tailnet integra e borda publica intocada."
